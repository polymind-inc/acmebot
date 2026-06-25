using Acmebot.App.Providers;
using Acmebot.App.Services;

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
        var service = new DnsZoneQueryService([provider]);

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
        var service = new DnsZoneQueryService([failingProvider, workingProvider]);

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
        var service = new DnsZoneQueryService([provider]);
        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetAllDnsZonesAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ListZonesAsync_WithUnknownProvider_ReturnsEmpty()
    {
        var provider = new TestDnsProvider("Test DNS", [CreateZone("example", "example.com")]);
        var service = new DnsZoneQueryService([provider]);

        var zones = await service.ListZonesAsync("Other DNS", TestContext.Current.CancellationToken);

        Assert.Empty(zones);
    }

    [Fact]
    public async Task ListZonesAsync_WithKnownProvider_ReturnsProviderZones()
    {
        var provider = new TestDnsProvider("Test DNS", [CreateZone("example", "example.com")]);
        var service = new DnsZoneQueryService([provider]);

        var zones = await service.ListZonesAsync("Test DNS", TestContext.Current.CancellationToken);

        var zone = Assert.Single(zones);
        Assert.Equal("example.com", zone.Name);
        Assert.Same(provider, zone.DnsProvider);
    }

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
