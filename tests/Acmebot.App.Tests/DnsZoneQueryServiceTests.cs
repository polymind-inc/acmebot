using Acmebot.App.Providers;
using Acmebot.App.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsZoneQueryServiceTests
{
    [Fact]
    public async Task GetAllDnsZonesAsync_SortsZonesWithinProviderGroup()
    {
        var provider = new TestDnsProvider(
            "Test DNS",
            [
                CreateZone("b", "b.example.com"),
                CreateZone("a", "a.example.com")
            ]);
        var service = CreateService(provider);

        var groups = await service.GetAllDnsZonesAsync(TestContext.Current.CancellationToken);

        var group = Assert.Single(groups);
        Assert.Equal("Test DNS", group.DnsProviderName);
        Assert.Equal(["a.example.com", "b.example.com"], group.DnsZones.Select(x => x.Name));
    }

    [Fact]
    public async Task GetAllDnsZonesAsync_WhenProviderThrows_ReturnsEmptyGroupForThatProvider()
    {
        var failingProvider = new TestDnsProvider("Broken DNS", exception: new InvalidOperationException("boom"));
        var workingProvider = new TestDnsProvider("Working DNS", [CreateZone("example", "example.com")]);
        var service = CreateService(failingProvider, workingProvider);

        var groups = await service.GetAllDnsZonesAsync(TestContext.Current.CancellationToken);

        Assert.Collection(
            groups,
            group =>
            {
                Assert.Equal("Broken DNS", group.DnsProviderName);
                Assert.Empty(group.DnsZones);
            },
            group =>
            {
                Assert.Equal("Working DNS", group.DnsProviderName);
                Assert.Equal(["example.com"], group.DnsZones.Select(x => x.Name));
            });
    }

    [Fact]
    public async Task GetAllDnsZonesAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        var provider = new TestDnsProvider("Test DNS", [CreateZone("example", "example.com")]);
        var service = CreateService(provider);
        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetAllDnsZonesAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ListZonesAsync_WithUnknownProvider_ThrowsPreconditionExceptionNamingConfiguredProviders()
    {
        var provider = new TestDnsProvider("Test DNS", [CreateZone("example", "example.com")]);
        var service = CreateService(provider);

        var exception = await Assert.ThrowsAsync<PreconditionException>(
            () => service.ListZonesAsync("Other DNS", TestContext.Current.CancellationToken));

        Assert.Contains("'Other DNS' is not configured", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Test DNS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListZonesAsync_WithKnownProvider_ReturnsProviderZones()
    {
        var provider = new TestDnsProvider("Test DNS", [CreateZone("example", "example.com")]);
        var service = CreateService(provider);

        var zones = await service.ListZonesAsync("Test DNS", TestContext.Current.CancellationToken);

        var zone = Assert.Single(zones);
        Assert.Equal("example.com", zone.Name);
        Assert.Same(provider, zone.DnsProvider);
    }

    private static DnsZoneQueryService CreateService(params IDnsProvider[] dnsProviders)
        => new(dnsProviders, NullLogger<DnsZoneQueryService>.Instance);

    private static DnsZone CreateZone(string id, string name)
    {
        var provider = new TestDnsProvider("Test DNS");

        return new DnsZone(provider)
        {
            Id = id,
            Name = name
        };
    }
}
