using System.Net;

using Acmebot.App.Options;

using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

namespace Acmebot.App.Providers;

public class AzureDnsProvider(AzureDnsOptions options, ArmEnvironment environment, TokenCredential credential) : IDnsProvider, IProtectedApexTxtProvider
{
    private const string ApexCreatedMetadataKey = "acmebot-ns-apex-created";
    private const int ApexWriteAttempts = 5;

    private readonly ArmClient _armClient = new(credential, options.SubscriptionId, new ArmClientOptions { Environment = environment });

    public string Name => "Azure DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);

        await foreach (var zone in subscription.GetDnsZonesAsync(cancellationToken: cancellationToken))
        {
            zones.Add(new DnsZone { Id = zone.Id.ToString(), Name = zone.Data.Name, NameServers = zone.Data.NameServers });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        relativeRecordName = ToAzureRelativeRecordName(relativeRecordName);

        // TXT レコードに値をセットする
        var txtRecordData = new DnsTxtRecordData
        {
            TtlInSeconds = 60
        };

        foreach (var value in values)
        {
            txtRecordData.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { value } });
        }

        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zone.Id));

        var dnsTxtRecords = dnsZoneResource.GetDnsTxtRecords();

        return dnsTxtRecords.CreateOrUpdateAsync(WaitUntil.Completed, relativeRecordName, txtRecordData, cancellationToken: cancellationToken);
    }

    public async Task AddApexTxtValuesAsync(DnsZone zone, string[] values, CancellationToken cancellationToken = default)
    {
        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zone.Id));
        var recordCollection = dnsZoneResource.GetDnsTxtRecords();

        for (var attempt = 0; attempt < ApexWriteAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = await GetApexRecordAsync(dnsZoneResource, cancellationToken);
            var recordData = existing?.Data ?? new DnsTxtRecordData { TtlInSeconds = 60 };

            if (existing is null)
            {
                // Keep ownership across workers so cleanup can delete only
                // record sets Acmebot actually created, not an existing empty @.
                recordData.Metadata[ApexCreatedMetadataKey] = "true";
            }
            else if (recordData.ETag is null)
            {
                throw new PreconditionException($"Azure DNS TXT record at the apex of zone '{zone.Name}' has no ETag; refusing an unconditional update.");
            }

            // A previously committed but unacknowledged write can be retried.
            // Reusing the same ACME token must therefore be idempotent.
            if (!AddApexTxtValues(recordData, values))
            {
                return;
            }

            try
            {
                // If-None-Match protects creates; If-Match protects updates.
                // A concurrent change causes a 409/412 and a fresh read/merge.
                await recordCollection.CreateOrUpdateAsync(
                    WaitUntil.Completed, "@", recordData,
                    ifMatch: existing?.Data.ETag,
                    ifNoneMatch: existing is null ? "*" : null,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                if (attempt == ApexWriteAttempts - 1)
                {
                    throw new PreconditionException($"Azure DNS TXT record at the apex of zone '{zone.Name}' kept changing; unable to add ACME values safely.", ex);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), cancellationToken);
            }
        }

        throw new InvalidOperationException("Unreachable Azure DNS apex retry state.");
    }

    public async Task RemoveApexTxtValuesAsync(DnsZone zone, string[] expectedValues, CancellationToken cancellationToken = default)
    {
        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zone.Id));
        var recordCollection = dnsZoneResource.GetDnsTxtRecords();

        for (var attempt = 0; attempt < ApexWriteAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recordSet = await GetApexRecordAsync(dnsZoneResource, cancellationToken);

            if (recordSet is null)
            {
                return;
            }

            var recordData = recordSet.Data;

            if (recordData.ETag is not { } etag)
            {
                throw new PreconditionException($"Azure DNS TXT record at the apex of zone '{zone.Name}' has no ETag; refusing an unconditional cleanup.");
            }

            // Leave all non-ACME TXT values, TTL, and metadata untouched.
            if (!RemoveApexTxtValues(recordData, expectedValues))
            {
                return;
            }

            try
            {
                if (IsAcmebotOwnedEmptyApexRecord(recordData))
                {
                    // Only delete an empty record set originally created by us.
                    await recordSet.DeleteAsync(WaitUntil.Completed, ifMatch: etag, cancellationToken: cancellationToken);
                }
                else
                {
                    // An existing TXT record set must survive even when empty.
                    await recordCollection.CreateOrUpdateAsync(
                        WaitUntil.Completed, "@", recordData, ifMatch: etag, cancellationToken: cancellationToken);
                }

                return;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // Another actor deleted the record after our read.
                return;
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                if (attempt == ApexWriteAttempts - 1)
                {
                    throw new PreconditionException($"Azure DNS TXT record at the apex of zone '{zone.Name}' kept changing; unable to clean up ACME values safely.", ex);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), cancellationToken);
            }
        }

        throw new InvalidOperationException("Unreachable Azure DNS apex cleanup retry state.");
    }

    private static async Task<DnsTxtRecordResource?> GetApexRecordAsync(DnsZoneResource zone, CancellationToken cancellationToken)
    {
        try
        {
            return (await zone.GetDnsTxtRecordAsync("@", cancellationToken)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // Each ACME token is a separate TXT record with one string segment.
    // Existing multi-segment TXT records are retained unchanged.
    internal static bool AddApexTxtValues(DnsTxtRecordData data, IEnumerable<string> values)
    {
        var existing = data.DnsTxtRecords.Where(x => x.Values.Count == 1)
                           .Select(x => x.Values[0]).ToHashSet(StringComparer.Ordinal);
        var modified = false;

        foreach (var value in values)
        {
            if (existing.Add(value))
            {
                data.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { value } });
                modified = true;
            }
        }

        return modified;
    }

    internal static bool RemoveApexTxtValues(DnsTxtRecordData data, IEnumerable<string> values)
    {
        var ours = values.ToHashSet(StringComparer.Ordinal);
        var matches = data.DnsTxtRecords.Where(x => x.Values.Count == 1 && ours.Contains(x.Values[0])).ToArray();

        foreach (var match in matches)
        {
            data.DnsTxtRecords.Remove(match);
        }

        return matches.Length > 0;
    }

    internal static bool IsAcmebotOwnedEmptyApexRecord(DnsTxtRecordData data)
        => data.DnsTxtRecords.Count == 0 &&
           data.Metadata.TryGetValue(ApexCreatedMetadataKey, out var marker) &&
           marker == "true";

    // ARM DNS TXT record names use '@' for the zone apex.
    internal static string ToAzureRelativeRecordName(string relativeRecordName)
        => string.IsNullOrEmpty(relativeRecordName) ? "@" : relativeRecordName;

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        relativeRecordName = ToAzureRelativeRecordName(relativeRecordName);

        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zone.Id));

        try
        {
            var dnsTxtRecordResource = await dnsZoneResource.GetDnsTxtRecordAsync(relativeRecordName, cancellationToken);

            await dnsTxtRecordResource.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // ignored
        }
    }
}
