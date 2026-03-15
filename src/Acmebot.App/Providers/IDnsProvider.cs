namespace Acmebot.App.Providers;

public interface IDnsProvider
{
    string Name { get; }
    TimeSpan PropagationDelay { get; }
    Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default);
    Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default);
    Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default);
}
