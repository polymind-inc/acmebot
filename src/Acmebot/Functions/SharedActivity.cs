using System.Net;
using System.Security.Cryptography.X509Certificates;

using Acmebot.Internal;
using Acmebot.Models;
using Acmebot.Options;
using Acmebot.Providers;

using ACMESharp.Authorizations;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using Azure.Security.KeyVault.Certificates;

using DnsClient;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace Acmebot.Functions;

public class SharedActivity(
    LookupClient lookupClient,
    AcmeProtocolClientFactory acmeProtocolClientFactory,
    IEnumerable<IDnsProvider> dnsProviders,
    CertificateClient certificateClient,
    WebhookInvoker webhookInvoker,
    IOptions<AcmebotOptions> options,
    ILogger<SharedActivity> logger)
{
    private readonly AcmebotOptions _options = options.Value;

    [Function(nameof(GetRenewalCertificates))]
    public async Task<IReadOnlyList<CertificateItem>> GetRenewalCertificates([ActivityTrigger] object input)
    {
        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        var certificateProperties = certificateClient.GetPropertiesOfCertificatesAsync();

        var result = new List<CertificateItem>();

        var now = DateTimeOffset.UtcNow;

        await foreach (var properties in certificateProperties)
        {
            // Acmebot で発行していない、ACME エンドポイントが異なる証明書は対象外とする
            if (!properties.IsIssuedByAcmebot() || !properties.IsSameEndpoint(_options.Endpoint))
            {
                continue;
            }

            // Key Vault から証明書を取得する
            var certificate = await certificateClient.GetCertificateAsync(properties.Name);

            // ACME Renewal Information が有効な場合は優先して使う
            if (acmeProtocolClient.Directory.RenewalInfo is not null)
            {
                var certificateId = X509CertificateLoader.LoadCertificate(certificate.Value.Cer).GetCertificateId();

                if (certificateId is not null)
                {
                    var renewalInfo = await acmeProtocolClient.GetRenewalInfoAsync(certificateId);

                    // 推奨ウィンドウが始まった場合は更新する
                    if (renewalInfo.SuggestedWindow.Start < now)
                    {
                        result.Add(certificate.Value.ToCertificateItem());

                        continue;
                    }
                }
            }

            // 期限が近い場合には更新する
            if ((properties.ExpiresOn.GetValueOrDefault(DateTimeOffset.MaxValue) - now).TotalDays <= _options.RenewBeforeExpiry)
            {
                result.Add(certificate.Value.ToCertificateItem());
            }
        }

        return result;
    }

    [Function(nameof(GetAllCertificates))]
    public async Task<IReadOnlyList<CertificateItem>> GetAllCertificates([ActivityTrigger] object input)
    {
        var certificates = certificateClient.GetPropertiesOfCertificatesAsync();

        var result = new List<CertificateItem>();

        await foreach (var certificate in certificates)
        {
            var certificateItem = (await certificateClient.GetCertificateAsync(certificate.Name)).Value.ToCertificateItem();

            certificateItem.IsIssuedByAcmebot = certificate.IsIssuedByAcmebot();
            certificateItem.IsSameEndpoint = certificate.IsSameEndpoint(_options.Endpoint);

            result.Add(certificateItem);
        }

        return result;
    }

    [Function(nameof(GetAllDnsZones))]
    public async Task<IReadOnlyList<DnsZoneGroup>> GetAllDnsZones([ActivityTrigger] object input)
    {
        try
        {
            var zones = await dnsProviders.ListAllZonesAsync();

            return zones.Select(x => new DnsZoneGroup
            {
                DnsProviderName = x.Item1,
                DnsZones = x.Item2.Select(xs => xs.ToDnsZoneItem()).OrderBy(xs => xs.Name).ToArray()
            }).ToArray();
        }
        catch
        {
            return [];
        }
    }

    [Function(nameof(GetCertificatePolicy))]
    public async Task<CertificatePolicyItem> GetCertificatePolicy([ActivityTrigger] string certificateName)
    {
        KeyVaultCertificateWithPolicy certificate = await certificateClient.GetCertificateAsync(certificateName);

        return certificate.ToCertificatePolicyItem();
    }

    [Function(nameof(RevokeCertificate))]
    public async Task RevokeCertificate([ActivityTrigger] string certificateName)
    {
        var response = await certificateClient.GetCertificateAsync(certificateName);

        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        await acmeProtocolClient.RevokeCertificateAsync(response.Value.Cer);
    }

    [Function(nameof(Order))]
    public async Task<OrderDetails> Order([ActivityTrigger] IReadOnlyList<string> dnsNames)
    {
        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        return await acmeProtocolClient.CreateOrderAsync(dnsNames, preferredProfile: _options.PreferredProfile);
    }

    [Function(nameof(Dns01Precondition))]
    public async Task<string> Dns01Precondition([ActivityTrigger] CertificatePolicyItem certificatePolicyItem)
    {
        // DNS zone の一覧を各 Provider から取得
        var zones = await dnsProviders.FlattenAllZonesAsync();

        // DNS zone が存在するか確認
        var foundZones = new HashSet<DnsZone>();
        var notFoundZoneDnsNames = new List<string>();

        foreach (var dnsName in certificatePolicyItem.AliasedDnsNames)
        {
            var zone = zones.FindDnsZone(dnsName);

            // マッチする DNS zone が見つからない場合はエラー
            if (zone is null)
            {
                notFoundZoneDnsNames.Add(dnsName);
                continue;
            }

            foundZones.Add(zone);
        }

        if (notFoundZoneDnsNames.Count > 0)
        {
            throw new PreconditionException($"DNS zone(s) are not found. DnsNames = {string.Join(",", notFoundZoneDnsNames)}");
        }

        // DNS zone に移譲されている Name servers が正しいか検証
        foreach (var zone in foundZones.Where(x => x.NameServers is { Count: > 0 }))
        {
            // DNS provider が Name servers を返している場合は NS レコードを確認
            var queryResult = await lookupClient.QueryAsync(zone.Name, QueryType.NS);

            // 最後の . が付いている場合があるので削除して統一
            var expectedNameServers = zone.NameServers
                                          .Select<string, string>(x => x.TrimEnd('.'))
                                          .ToArray();

            var actualNameServers = queryResult.Answers
                                               .OfType<DnsClient.Protocol.NsRecord>()
                                               .Select(x => x.NSDName.Value.TrimEnd('.'))
                                               .ToArray();

            // 処理対象の DNS zone から取得した NS と実際に引いた NS の値が一つも一致しない場合はエラー
            if (!actualNameServers.Intersect(expectedNameServers, StringComparer.OrdinalIgnoreCase).Any())
            {
                throw new PreconditionException($"The delegated name server is not correct. DNS zone = {zone.Name}, Expected = {string.Join(",", expectedNameServers)}, Actual = {string.Join(",", actualNameServers)}");
            }
        }

        // 指定された DNS Provider に属する DNS zone を優先する
        var dnsProvider = foundZones.Select(x => x.DnsProvider).FirstOrDefault(x => x.Name == certificatePolicyItem.DnsProviderName);

        // DNS zone の属する Provider が変わった可能性があるのでフォールバック
        if (dnsProvider is null)
        {
            // 見つかった DNS zone の属する DNS Provider を取得
            var foundDnsProviders = foundZones.Select(x => x.DnsProvider).DistinctBy(x => x.Name).ToArray();

            // 単一の DNS Provider で構成された証明書かチェックする
            if (foundDnsProviders.Length != 1)
            {
                // 互換性のために常に空文字列を返す
                return "";
            }

            // 単一の DNS Provider で構成されている場合は問題ない
            dnsProvider = dnsProviders.First();
        }

        return dnsProvider.Name;
    }

    [Function(nameof(Dns01Authorization))]
    public async Task<(IReadOnlyList<AcmeChallengeResult>, int)> Dns01Authorization([ActivityTrigger] (string, string?, IReadOnlyList<string>) input)
    {
        var (dnsProviderName, dnsAlias, authorizationUrls) = input;

        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        var challengeResults = new List<AcmeChallengeResult>();

        foreach (var authorizationUrl in authorizationUrls)
        {
            // Authorization の詳細を取得
            var authorization = await acmeProtocolClient.GetAuthorizationDetailsAsync(authorizationUrl);

            // ignore authorizations that are already valid 
            if (authorization.Status == "valid")
            {
                continue;
            }

            // DNS-01 Challenge の情報を拾う
            var challenge = authorization.Challenges.FirstOrDefault<Challenge>(x => x.Type == "dns-01");

            if (challenge is null)
            {
                throw new PreconditionException("DNS-01 cannot be used for domains for which a certificate has already been issued using HTTP-01.");
            }

            var challengeValidationDetails = AuthorizationDecoder.ResolveChallengeForDns01(authorization, challenge, acmeProtocolClient.Signer);

            // Challenge の情報を保存する
            challengeResults.Add(new AcmeChallengeResult
            {
                Url = challenge.Url,
                DnsRecordName = string.IsNullOrEmpty(dnsAlias) ? challengeValidationDetails.DnsRecordName : $"_acme-challenge.{dnsAlias}",
                DnsRecordValue = challengeValidationDetails.DnsRecordValue
            });
        }

        // DNS zone の一覧を各 Provider から取得
        var zones = (await (string.IsNullOrEmpty(dnsProviderName) ? dnsProviders.FlattenAllZonesAsync() : dnsProviders.ListZonesAsync(dnsProviderName)));

        var propagationSeconds = 0;

        // DNS-01 の検証レコード名毎に DNS に TXT レコードを作成
        foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
        {
            var dnsRecordName = lookup.Key;

            var zone = zones.FindDnsZone(dnsRecordName);

            if (zone is null)
            {
                throw new PreconditionException($"DNS zone is not found. DnsRecordName = {dnsRecordName}");
            }

            // Challenge の詳細から DNS 向けにレコード名を作成
            var acmeDnsRecordName = dnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

            await zone.DnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName);
            await zone.DnsProvider.CreateTxtRecordAsync(zone, acmeDnsRecordName, lookup.Select(x => x.DnsRecordValue));

            // 一番時間のかかる DNS Provider に合わせる
            propagationSeconds = Math.Max(propagationSeconds, zone.DnsProvider.PropagationSeconds);
        }

        return (challengeResults, propagationSeconds);
    }

    [Function(nameof(CheckDnsChallenge))]
    public async Task CheckDnsChallenge([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
    {
        foreach (var challengeResult in challengeResults)
        {
            IDnsQueryResponse queryResult;

            try
            {
                // 実際に ACME の TXT レコードを引いて確認する
                queryResult = await lookupClient.QueryAsync(challengeResult.DnsRecordName, QueryType.TXT);
            }
            catch (DnsResponseException ex)
            {
                // 一時的な DNS エラーの可能性があるためリトライ
                throw new RetriableActivityException($"{challengeResult.DnsRecordName} bad response. Message: \"{ex.DnsError}\"", ex);
            }

            var txtRecords = queryResult.Answers
                                        .OfType<DnsClient.Protocol.TxtRecord>()
                                        .ToArray();

            // レコードが存在しなかった場合はエラー
            if (txtRecords.Length == 0)
            {
                throw new RetriableActivityException($"{challengeResult.DnsRecordName} did not resolve.");
            }

            // レコードに今回のチャレンジが含まれていない場合もエラー
            if (!txtRecords.Any(x => x.Text.Contains(challengeResult.DnsRecordValue)))
            {
                throw new RetriableActivityException($"{challengeResult.DnsRecordName} is not correct. Expected: \"{challengeResult.DnsRecordValue}\", Actual: \"{string.Join(",", txtRecords.SelectMany(x => x.Text))}\"");
            }
        }
    }

    [Function(nameof(AnswerChallenges))]
    public async Task AnswerChallenges([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
    {
        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        // Answer の準備が出来たことを通知
        foreach (var challengeResult in challengeResults)
        {
            await acmeProtocolClient.AnswerChallengeAsync(challengeResult.Url);
        }
    }

    [Function(nameof(CheckIsReady))]
    public async Task CheckIsReady([ActivityTrigger] (OrderDetails, IReadOnlyList<AcmeChallengeResult>) input)
    {
        var (orderDetails, challengeResults) = input;

        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        orderDetails = await acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

        if (orderDetails.Payload.Status == "invalid")
        {
            var problems = new List<Problem>();

            foreach (var challengeResult in challengeResults)
            {
                var challenge = await acmeProtocolClient.GetChallengeDetailsAsync(challengeResult.Url);

                if (challenge.Status != "invalid" || challenge.Error is null)
                {
                    continue;
                }

                logger.LogError("ACME domain validation error: {SerializeObject}", JsonConvert.SerializeObject(challenge.Error));

                problems.Add(challenge.Error);
            }

            // 全てのエラーが dns 関係の場合は Orchestrator からリトライさせる
            if (problems.All(x => x.Type == "urn:ietf:params:acme:error:dns"))
            {
                throw new RetriableOrchestratorException("ACME validation status is invalid, but retriable error. It will retry automatically.");
            }

            // invalid の場合は最初から実行が必要なので失敗させる
            throw new InvalidOperationException($"ACME validation status is invalid. Required retry at first.\nLastError = {JsonConvert.SerializeObject(problems.Last())}");
        }

        if (orderDetails.Payload.Status != "ready")
        {
            // ready 以外の場合はリトライする
            throw new RetriableActivityException($"ACME validation status is {orderDetails.Payload.Status}. It will retry automatically.");
        }
    }

    [Function(nameof(FinalizeOrder))]
    public async Task<OrderDetails> FinalizeOrder([ActivityTrigger] (CertificatePolicyItem, OrderDetails) input)
    {
        var (certificatePolicyItem, orderDetails) = input;

        byte[] csr;

        try
        {
            var certificatePolicy = certificatePolicyItem.ToCertificatePolicy();
            var metadata = certificatePolicyItem.ToCertificateMetadata(_options.Endpoint);

            var certificateOperation = await certificateClient.StartCreateCertificateAsync(certificatePolicyItem.CertificateName, certificatePolicy, tags: metadata);

            csr = certificateOperation.Properties.Csr;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
        {
            var certificateOperation = await certificateClient.GetCertificateOperationAsync(certificatePolicyItem.CertificateName);

            csr = certificateOperation.Properties.Csr;
        }

        // Order の最終処理を実行する
        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        return await acmeProtocolClient.FinalizeOrderAsync(orderDetails.Payload.Finalize, csr);
    }

    [Function(nameof(CheckIsValid))]
    public async Task<OrderDetails> CheckIsValid([ActivityTrigger] OrderDetails orderDetails)
    {
        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        orderDetails = await acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

        if (orderDetails.Payload.Status == "invalid")
        {
            // invalid の場合は最初から実行が必要なので失敗させる
            throw new InvalidOperationException("Finalize request is invalid. Required retry at first.");
        }

        if (orderDetails.Payload.Status != "valid")
        {
            // valid 以外の場合はリトライする
            throw new RetriableActivityException($"Finalize request is {orderDetails.Payload.Status}. It will retry automatically.");
        }

        return orderDetails;
    }

    [Function(nameof(MergeCertificate))]
    public async Task<CertificateItem> MergeCertificate([ActivityTrigger] (string, OrderDetails) input)
    {
        var (certificateName, orderDetails) = input;

        var acmeProtocolClient = await acmeProtocolClientFactory.CreateClientAsync();

        // 証明書をダウンロードして Key Vault へ格納
        var x509Certificates = await acmeProtocolClient.GetOrderCertificateAsync(orderDetails, _options.PreferredChain);

        var mergeCertificateOptions = new MergeCertificateOptions(
            certificateName,
            [x509Certificates.Export(X509ContentType.Pfx)]
        );

        return (await certificateClient.MergeCertificateAsync(mergeCertificateOptions)).Value.ToCertificateItem();
    }

    [Function(nameof(CleanupDnsChallenge))]
    public async Task CleanupDnsChallenge([ActivityTrigger] (string, IReadOnlyList<AcmeChallengeResult>) input)
    {
        var (dnsProviderName, challengeResults) = input;

        // DNS zone の一覧を各 Provider から取得
        var zones = (await (string.IsNullOrEmpty(dnsProviderName) ? dnsProviders.FlattenAllZonesAsync() : dnsProviders.ListZonesAsync(dnsProviderName)));

        // DNS-01 の検証レコード名毎に DNS から TXT レコードを削除
        foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
        {
            var dnsRecordName = lookup.Key;

            var zone = zones.FindDnsZone(dnsRecordName);

            if (zone is null)
            {
                continue;
            }

            // Challenge の詳細から DNS 向けにレコード名を作成
            var acmeDnsRecordName = dnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

            await zone.DnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName);
        }
    }

    [Function(nameof(SendCompletedEvent))]
    public Task SendCompletedEvent([ActivityTrigger] (string, DateTimeOffset?, IReadOnlyList<string>) input)
    {
        var (certificateName, expirationDate, dnsNames) = input;

        return webhookInvoker.SendCompletedEventAsync(certificateName, expirationDate, dnsNames, _options.Endpoint.Host);
    }

    [Function(nameof(SendFailedEvent))]
    public Task SendFailedEvent([ActivityTrigger] (string, IReadOnlyList<string>) input)
    {
        var (certificateName, dnsNames) = input;

        return webhookInvoker.SendFailedEventAsync(certificateName, dnsNames);
    }
}
