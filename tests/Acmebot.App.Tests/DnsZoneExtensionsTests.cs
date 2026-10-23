using Acmebot.App.Extensions;
using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsZoneExtensionsTests
{
    [Theory]
    [InlineData("staging.example.com")]
    [InlineData("www.staging.example.com")]
    public void FindDnsZone_WithParentAndChildZones_ReturnsMostSpecificZone(string dnsName)
    {
        var zones = new[]
        {
            CreateZone("root", "example.com"),
            CreateZone("staging", "staging.example.com")
        };

        var zone = zones.FindDnsZone(dnsName);

        Assert.NotNull(zone);
        Assert.Equal("staging.example.com", zone.Name);
    }

    [Fact]
    public void FindDnsZone_WithDifferentCasing_ReturnsMatchingZone()
    {
        var zones = new[] { CreateZone("root", "example.com") };

        var zone = zones.FindDnsZone("WWW.EXAMPLE.COM");

        Assert.NotNull(zone);
        Assert.Equal("example.com", zone.Name);
    }

    [Fact]
    public void FindDnsZone_WithNonBoundarySuffix_ReturnsNull()
    {
        var zones = new[] { CreateZone("root", "example.com") };

        var zone = zones.FindDnsZone("badexample.com");

        Assert.Null(zone);
    }

    private static DnsZone CreateZone(string id, string name) => new()
    {
        Id = id,
        Name = name
    };
}
