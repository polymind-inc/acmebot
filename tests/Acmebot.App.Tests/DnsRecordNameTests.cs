using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsRecordNameTests
{
    [Theory]
    [InlineData("example.com", "_acme-challenge", "_acme-challenge.example.com")]
    [InlineData("example.com.", "_acme-challenge", "_acme-challenge.example.com")]
    [InlineData("example.com", "_acme-challenge.", "_acme-challenge.example.com")]
    [InlineData("example.com", "_acme-challenge.www", "_acme-challenge.www.example.com")]
    public void ToFqdn_NormalizesTrailingDots(string zoneName, string relativeRecordName, string expected)
    {
        Assert.Equal(expected, DnsRecordName.ToFqdn(zoneName, relativeRecordName));
    }

    [Theory]
    [InlineData("example.com", "_acme-challenge", "_acme-challenge.example.com.")]
    [InlineData("example.com.", "_acme-challenge", "_acme-challenge.example.com.")]
    public void ToAbsoluteFqdn_AlwaysEndsWithTrailingDot(string zoneName, string relativeRecordName, string expected)
    {
        Assert.Equal(expected, DnsRecordName.ToAbsoluteFqdn(zoneName, relativeRecordName));
    }
}
