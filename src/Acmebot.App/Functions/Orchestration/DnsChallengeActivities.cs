using Acmebot.Acme.Challenges;
using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Providers;
using Acmebot.App.Services;

using DnsClient;

using Microsoft.Azure.Functions.Worker;

namespace Acmebot.App.Functions.Orchestration;

public class DnsChallengeActivities(
    LookupClient lookupClient,
    AcmeClientFactory acmeClientFactory,
    DnsZoneQueryService dnsZoneQueryService)
{
    [Function(nameof(Dns01Precondition))]
    public async Task<string> Dns01Precondition([ActivityTrigger] CertificatePolicyItem certificatePolicyItem)
    {
        var zones = await dnsZoneQueryService.ListZonesAsync(certificatePolicyItem.DnsProviderName);

        var foundZones = new HashSet<DnsZone>();
        var notFoundZoneDnsNames = new List<string>();

        foreach (var dnsName in certificatePolicyItem.AliasedDnsNames)
        {
            var zone = zones.FindDnsZone(dnsName);

            if (zone is null)
            {
                notFoundZoneDnsNames.Add(dnsName);
                continue;
            }

            foundZones.Add(zone);
        }

        if (notFoundZoneDnsNames.Count > 0)
        {
            throw new PreconditionException($"No DNS zone was found for the following domain name(s): {string.Join(", ", notFoundZoneDnsNames)}.");
        }

        foreach (var zone in foundZones.Where(x => x.NameServers is { Count: > 0 }))
        {
            var queryResult = await lookupClient.QueryAsync(zone.Name, QueryType.NS);

            var expectedNameServers = zone.NameServers
                                          .Select<string, string>(x => x.TrimEnd('.'))
                                          .ToArray();

            var actualNameServers = queryResult.Answers
                                               .OfType<DnsClient.Protocol.NsRecord>()
                                               .Select(x => x.NSDName.Value.TrimEnd('.'))
                                               .ToArray();

            if (!actualNameServers.Intersect(expectedNameServers, StringComparer.OrdinalIgnoreCase).Any())
            {
                throw new PreconditionException($"The delegated name servers for DNS zone '{zone.Name}' do not match the expected configuration. Expected: {string.Join(", ", expectedNameServers)}. Actual: {string.Join(", ", actualNameServers)}.");
            }
        }

        return certificatePolicyItem.DnsProviderName;
    }

    [Function(nameof(Dns01Authorization))]
    public async Task<(IReadOnlyList<AcmeChallengeResult>, int)> Dns01Authorization([ActivityTrigger] (string, string?, IReadOnlyList<Uri>) input, CancellationToken cancellationToken)
    {
        var (dnsProviderName, dnsAlias, authorizationUrls) = input;

        var acmeContext = await acmeClientFactory.CreateClientAsync();
        var acmeClient = acmeContext.Client;

        var challengeResults = new List<AcmeChallengeResult>();

        foreach (var authorizationUrl in authorizationUrls)
        {
            var authorization = (await acmeClient.GetAuthorizationAsync(acmeContext.Account, authorizationUrl, cancellationToken)).Resource;

            if (authorization.Status == AcmeAuthorizationStatuses.Valid)
            {
                continue;
            }

            var challenge = authorization.Challenges.FirstOrDefault(x => x.Type == AcmeChallengeTypes.Dns01);

            if (challenge is null)
            {
                throw new PreconditionException("DNS-01 validation cannot be used for domains that have already been validated with HTTP-01.");
            }

            var challengeInstruction = AcmeChallengeInstructions.CreateDns01(acmeContext.Account, authorization, challenge);

            challengeResults.Add(new AcmeChallengeResult
            {
                Url = challenge.Url,
                DnsRecordName = string.IsNullOrEmpty(dnsAlias) ? challengeInstruction.RecordName.TrimEnd('.') : $"_acme-challenge.{dnsAlias}",
                DnsRecordValue = challengeInstruction.RecordValue
            });
        }

        var zones = await dnsZoneQueryService.ListZonesAsync(dnsProviderName, cancellationToken);

        var propagationSeconds = 0;

        foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
        {
            var dnsRecordName = lookup.Key;

            var zone = zones.FindDnsZone(dnsRecordName);

            if (zone is null)
            {
                throw new PreconditionException($"No DNS zone was found for record '{dnsRecordName}'.");
            }

            var acmeDnsRecordName = GetRelativeRecordName(dnsRecordName, zone);

            await zone.DnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName, cancellationToken);
            await zone.DnsProvider.CreateTxtRecordAsync(zone, acmeDnsRecordName, lookup.Select(x => x.DnsRecordValue).ToArray(), cancellationToken);

            propagationSeconds = Math.Max(propagationSeconds, (int)zone.DnsProvider.PropagationDelay.TotalSeconds);
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
                queryResult = await lookupClient.QueryAsync(challengeResult.DnsRecordName, QueryType.TXT);
            }
            catch (DnsResponseException ex)
            {
                throw new RetriableActivityException($"DNS query for '{challengeResult.DnsRecordName}' returned an error response: {ex.DnsError}.", ex);
            }

            var txtRecords = queryResult.Answers
                                        .OfType<DnsClient.Protocol.TxtRecord>()
                                        .ToArray();

            if (txtRecords.Length == 0)
            {
                throw new RetriableActivityException($"DNS query for '{challengeResult.DnsRecordName}' did not return any TXT records yet.");
            }

            if (!txtRecords.Any(x => x.Text.Contains(challengeResult.DnsRecordValue)))
            {
                throw new RetriableActivityException($"DNS TXT record '{challengeResult.DnsRecordName}' does not contain the expected value. Expected: '{challengeResult.DnsRecordValue}'. Actual: '{string.Join(", ", txtRecords.SelectMany(x => x.Text))}'.");
            }
        }
    }

    [Function(nameof(CleanupDnsChallenge))]
    public async Task CleanupDnsChallenge([ActivityTrigger] (string, IReadOnlyList<AcmeChallengeResult>) input, CancellationToken cancellationToken)
    {
        var (dnsProviderName, challengeResults) = input;

        var zones = await dnsZoneQueryService.ListZonesAsync(dnsProviderName, cancellationToken);

        foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
        {
            var dnsRecordName = lookup.Key;

            var zone = zones.FindDnsZone(dnsRecordName);

            if (zone is null)
            {
                continue;
            }

            var acmeDnsRecordName = GetRelativeRecordName(dnsRecordName, zone);

            await zone.DnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName, cancellationToken);
        }
    }

    private static string GetRelativeRecordName(string dnsRecordName, DnsZone zone)
    {
        if (string.Equals(dnsRecordName, zone.Name, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        var zoneSuffix = $".{zone.Name}";

        return dnsRecordName.EndsWith(zoneSuffix, StringComparison.OrdinalIgnoreCase)
            ? dnsRecordName[..^zoneSuffix.Length]
            : dnsRecordName;
    }
}
