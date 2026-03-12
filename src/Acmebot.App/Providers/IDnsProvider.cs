namespace Acmebot.App.Providers;

public interface IDnsProvider
{
    string Name { get; }
    int PropagationSeconds { get; }
    Task<IReadOnlyList<DnsZone>> ListZonesAsync();
    Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values);
    Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName);
}
