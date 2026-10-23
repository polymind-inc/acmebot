using System.Text.Json;

using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsZoneTests
{
    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("例え.テスト", "xn--r8jz45g.xn--zckzah")]
    public void Serialization_RoundTripsZoneDataWithoutProviderState(string name, string expectedName)
    {
        var zone = new DnsZone { Id = "zone-id", Name = name, NameServers = ["ns1.example.com", "ns2.example.com"] };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, IncludeFields = true };

        var json = JsonSerializer.Serialize(zone, options);
        var restored = JsonSerializer.Deserialize<DnsZone>(json, options);

        Assert.NotNull(restored);
        Assert.Equal(zone.Id, restored.Id);
        Assert.Equal(expectedName, restored.Name);
        Assert.Equal(zone.NameServers, restored.NameServers);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(3, document.RootElement.EnumerateObject().Count());
        Assert.False(document.RootElement.TryGetProperty("dnsProvider", out _));
    }
}
