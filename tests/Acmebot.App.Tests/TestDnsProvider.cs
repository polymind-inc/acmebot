using Acmebot.App.Providers;

namespace Acmebot.App.Tests;

internal sealed class TestDnsProvider(string name, IReadOnlyList<DnsZone>? zoneDefinitions = null, Exception? exception = null, TimeSpan? propagationDelay = null) : IDnsProvider
{
    public string Name { get; } = name;

    public TimeSpan PropagationDelay { get; } = propagationDelay ?? TimeSpan.Zero;

    public Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (exception is not null)
        {
            throw exception;
        }

        return Task.FromResult<IReadOnlyList<DnsZone>>(zoneDefinitions?.Select(CreateZone).ToArray() ?? []);
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private DnsZone CreateZone(DnsZone zoneDefinition)
    {
        return new DnsZone(this)
        {
            Id = zoneDefinition.Id,
            Name = zoneDefinition.Name,
            NameServers = zoneDefinition.NameServers
        };
    }
}
