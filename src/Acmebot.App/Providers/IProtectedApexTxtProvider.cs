namespace Acmebot.App.Providers;

/// <summary>
/// Adds and removes only Acmebot's challenge TXT values at NS-delegated zone
/// apexes, using conditional Azure DNS writes to preserve unrelated values.
/// </summary>
public interface IProtectedApexTxtProvider
{
    Task AddApexTxtValuesAsync(DnsZone zone, string[] values, CancellationToken cancellationToken = default);

    Task RemoveApexTxtValuesAsync(DnsZone zone, string[] values, CancellationToken cancellationToken = default);
}
