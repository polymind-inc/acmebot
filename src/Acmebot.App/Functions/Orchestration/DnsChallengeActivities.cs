using Acmebot.Acme.Challenges;
using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Providers;

using DnsClient;

using Microsoft.Azure.Functions.Worker;

namespace Acmebot.App.Functions.Orchestration;

public class DnsChallengeActivities(
    LookupClient lookupClient,
    AcmeClientFactory acmeClientFactory,
    IEnumerable<IDnsProvider> dnsProviders)
{
    [Function(nameof(GetAllDnsZones))]
    public async Task<IReadOnlyList<DnsZoneGroup>> GetAllDnsZones([ActivityTrigger] object input)
    {
        try
        {
            var zones = await dnsProviders.ListAllZonesAsync();

            return zones.Where(x => x.Item2 is not null)
                        .Select(x => new DnsZoneGroup
                        {
                            DnsProviderName = x.Item1,
                            DnsZones = x.Item2!.Select(xs => xs.ToDnsZoneItem()).OrderBy(xs => xs.Name).ToArray()
                        }).ToArray();
        }
        catch
        {
            return [];
        }
    }

    [Function(nameof(Dns01Precondition))]
    public async Task<string> Dns01Precondition([ActivityTrigger] CertificatePolicyItem certificatePolicyItem)
    {
        var zones = await dnsProviders.FlattenAllZonesAsync();

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

        var dnsProvider = foundZones.Select(x => x.DnsProvider).FirstOrDefault(x => x.Name == certificatePolicyItem.DnsProviderName);

        if (dnsProvider is null)
        {
            var foundDnsProviders = foundZones.Select(x => x.DnsProvider).DistinctBy(x => x.Name).ToArray();

            if (foundDnsProviders.Length != 1)
            {
                return "";
            }

            dnsProvider = foundDnsProviders[0];
        }

        return dnsProvider.Name;
    }

    [Function(nameof(Dns01Authorization))]
    public async Task<(IReadOnlyList<AcmeChallengeResult>, int)> Dns01Authorization([ActivityTrigger] (string, string?, IReadOnlyList<Uri>) input, CancellationToken cancellationToken)
    {
        var (dnsProviderName, dnsAlias, authorizationUrls) = input;

        using var acmeContext = await acmeClientFactory.CreateClientAsync();
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

        var zones = await (string.IsNullOrEmpty(dnsProviderName) ? dnsProviders.FlattenAllZonesAsync(cancellationToken) : dnsProviders.ListZonesAsync(dnsProviderName, cancellationToken));

        var propagationSeconds = 0;

        foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
        {
            var dnsRecordName = lookup.Key;

            var zone = zones.FindDnsZone(dnsRecordName);

            if (zone is null)
            {
                throw new PreconditionException($"No DNS zone was found for record '{dnsRecordName}'.");
            }

            var acmeDnsRecordName = dnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

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

        var zones = await (string.IsNullOrEmpty(dnsProviderName) ? dnsProviders.FlattenAllZonesAsync(cancellationToken) : dnsProviders.ListZonesAsync(dnsProviderName, cancellationToken));

        foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
        {
            var dnsRecordName = lookup.Key;

            var zone = zones.FindDnsZone(dnsRecordName);

            if (zone is null)
            {
                continue;
            }

            var acmeDnsRecordName = dnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

            await zone.DnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName, cancellationToken);
        }
    }
}
