using Acmebot.App.Extensions;
using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsZoneExtensionsTests
{
    [Fact]
    public void FindDnsZone_WithNestedZones_ReturnsLongestSuffixMatch()
    {
        var provider = new TestDnsProvider("Test DNS");
        var zones = new[]
        {
            CreateZone(provider, "root", "example.com"),
            CreateZone(provider, "sub", "api.example.com")
        };

        var zone = zones.FindDnsZone("www.api.example.com");

        Assert.NotNull(zone);
        Assert.Equal("api.example.com", zone.Name);
    }

    [Fact]
    public void FindDnsZone_WithDifferentCasing_ReturnsMatchingZone()
    {
        var provider = new TestDnsProvider("Test DNS");
        var zones = new[] { CreateZone(provider, "root", "example.com") };

        var zone = zones.FindDnsZone("WWW.EXAMPLE.COM");

        Assert.NotNull(zone);
        Assert.Equal("example.com", zone.Name);
    }

    [Fact]
    public void FindDnsZone_WithNonBoundarySuffix_ReturnsNull()
    {
        var provider = new TestDnsProvider("Test DNS");
        var zones = new[] { CreateZone(provider, "root", "example.com") };

        var zone = zones.FindDnsZone("badexample.com");

        Assert.Null(zone);
    }

    private static DnsZone CreateZone(TestDnsProvider provider, string id, string name) => new(provider)
    {
        Id = id,
        Name = name
    };
}
