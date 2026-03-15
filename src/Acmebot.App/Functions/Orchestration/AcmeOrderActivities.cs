using System.Net;
using System.Security.Cryptography.X509Certificates;

using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Options;

using Azure.Security.KeyVault.Certificates;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace Acmebot.App.Functions.Orchestration;

public partial class AcmeOrderActivities(
    AcmeClientFactory acmeClientFactory,
    CertificateClient certificateClient,
    IOptions<AcmebotOptions> options,
    ILogger<AcmeOrderActivities> logger)
{
    private readonly AcmebotOptions _options = options.Value;

    [Function(nameof(Order))]
    public async Task<OrderDetails> Order([ActivityTrigger] IReadOnlyList<string> dnsNames)
    {
        using var acmeContext = await acmeClientFactory.CreateClientAsync();

        var result = await acmeContext.Client.CreateOrderAsync(
            acmeContext.Account,
            dnsNames.Select(x => new AcmeIdentifier
            {
                Type = AcmeIdentifierTypes.Dns,
                Value = x
            }).ToArray(),
            profile: _options.PreferredProfile);

        return OrderDetails.FromResult(result);
    }

    [Function(nameof(AnswerChallenges))]
    public async Task AnswerChallenges([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
    {
        using var acmeContext = await acmeClientFactory.CreateClientAsync();

        foreach (var challengeResult in challengeResults)
        {
            await acmeContext.Client.AnswerChallengeAsync(acmeContext.Account, challengeResult.Url);
        }
    }

    [Function(nameof(CheckIsReady))]
    public async Task CheckIsReady([ActivityTrigger] (OrderDetails, IReadOnlyList<AcmeChallengeResult>) input)
    {
        var (orderDetails, challengeResults) = input;

        using var acmeContext = await acmeClientFactory.CreateClientAsync();
        var acmeClient = acmeContext.Client;

        orderDetails = OrderDetails.FromResult(await acmeClient.GetOrderAsync(acmeContext.Account, orderDetails.OrderUrl), orderDetails.OrderUrl);

        if (orderDetails.Payload.Status == AcmeOrderStatuses.Invalid)
        {
            var problems = new List<AcmeProblemDetails>();

            foreach (var challengeResult in challengeResults)
            {
                var challenge = (await acmeClient.GetChallengeAsync(acmeContext.Account, challengeResult.Url)).Resource;

                if (challenge.Status != AcmeChallengeStatuses.Invalid || challenge.Error is null)
                {
                    continue;
                }

                LogAcmeDomainValidationError(logger, JsonConvert.SerializeObject(challenge.Error));

                problems.Add(challenge.Error);
            }

            if (problems.Count > 0 && problems.All(x => x.Type is { } type && type == AcmeProblemTypes.Dns))
            {
                throw new RetriableOrchestratorException("ACME validation failed because of a DNS-related error. The operation will be retried automatically.");
            }

            throw new InvalidOperationException($"ACME validation failed and the order is now invalid. Review the reported problem and retry the operation.\nLast problem: {JsonConvert.SerializeObject(problems.Last())}");
        }

        if (orderDetails.Payload.Status != AcmeOrderStatuses.Ready)
        {
            throw new RetriableActivityException($"ACME validation is still in progress. Current order status: {orderDetails.Payload.Status}. The operation will be retried automatically.");
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

        using var acmeContext = await acmeClientFactory.CreateClientAsync();

        return OrderDetails.FromResult(
            await acmeContext.Client.FinalizeOrderAsync(
                acmeContext.Account,
                orderDetails.Payload.Finalize ?? throw new InvalidOperationException("The ACME order did not include a finalize URL."),
                csr),
            orderDetails.OrderUrl);
    }

    [Function(nameof(CheckIsValid))]
    public async Task<OrderDetails> CheckIsValid([ActivityTrigger] OrderDetails orderDetails)
    {
        using var acmeContext = await acmeClientFactory.CreateClientAsync();

        orderDetails = OrderDetails.FromResult(await acmeContext.Client.GetOrderAsync(acmeContext.Account, orderDetails.OrderUrl), orderDetails.OrderUrl);

        if (orderDetails.Payload.Status == AcmeOrderStatuses.Invalid)
        {
            throw new InvalidOperationException("The ACME order became invalid during finalization. Review the reported problem and retry the operation.");
        }

        if (orderDetails.Payload.Status != AcmeOrderStatuses.Valid)
        {
            throw new RetriableActivityException($"ACME order finalization is still in progress. Current order status: {orderDetails.Payload.Status}. The operation will be retried automatically.");
        }

        return orderDetails;
    }

    [Function(nameof(MergeCertificate))]
    public async Task<CertificateItem> MergeCertificate([ActivityTrigger] (string, OrderDetails) input)
    {
        var (certificateName, orderDetails) = input;

        using var acmeContext = await acmeClientFactory.CreateClientAsync();

        var x509Certificates = await acmeContext.Client.GetOrderCertificateAsync(acmeContext.Account, orderDetails, _options.PreferredChain);

        var mergeCertificateOptions = new MergeCertificateOptions(
            certificateName,
            [x509Certificates.Export(X509ContentType.Pfx)]
        );

        return (await certificateClient.MergeCertificateAsync(mergeCertificateOptions)).Value.ToCertificateItem();
    }

    [LoggerMessage(LogLevel.Error, "ACME domain validation failed. ProblemDetails: {ProblemDetailsJson}")]
    private static partial void LogAcmeDomainValidationError(ILogger logger, string problemDetailsJson);
}
