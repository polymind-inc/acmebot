using Acmebot.App.Extensions;
using Acmebot.App.Models;

using Azure.Security.KeyVault.Certificates;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateExtensionsTests
{
    private static readonly Uri s_endpoint = new("https://acme-v02.api.letsencrypt.org/directory");

    [Fact]
    public void IsIssuedByAcmebot_WithMetadataTag_ReturnsTrue()
    {
        var properties = CreateCertificateProperties(("Acmebot", """{"endpoint":"acme-v02.api.letsencrypt.org"}"""));

        Assert.True(properties.IsIssuedByAcmebot());
    }

    [Fact]
    public void IsIssuedByAcmebot_WithLegacyIssuerTag_ReturnsTrue()
    {
        var properties = CreateCertificateProperties(("Issuer", "Acmebot"));

        Assert.True(properties.IsIssuedByAcmebot());
    }

    [Fact]
    public void IsIssuedByAcmebot_WithNonAcmebotLegacyIssuerTag_ReturnsFalse()
    {
        var properties = CreateCertificateProperties(("Issuer", "Other"));

        Assert.False(properties.IsIssuedByAcmebot());
    }

    [Theory]
    [InlineData("""{"endpoint":"acme-v02.api.letsencrypt.org"}""")]
    [InlineData("""{"endpoint":"https://acme-v02.api.letsencrypt.org/directory"}""")]
    public void IsSameEndpoint_WithMetadataEndpoint_ReturnsTrue(string metadata)
    {
        var properties = CreateCertificateProperties(("Acmebot", metadata));

        Assert.True(properties.IsSameEndpoint(s_endpoint));
    }

    [Fact]
    public void IsSameEndpoint_WithInvalidMetadataJson_FallsBackToLegacyEndpointTag()
    {
        var properties = CreateCertificateProperties(
            ("Acmebot", "{"),
            ("Issuer", "Acmebot"),
            ("Endpoint", "https://acme-v02.api.letsencrypt.org/directory"));

        Assert.True(properties.IsSameEndpoint(s_endpoint));
    }

    [Fact]
    public void TryGetCertificateId_WithCertificateIdInMetadata_ReturnsTrue()
    {
        var properties = CreateCertificateProperties(("Acmebot", """{"certificateId":"abc123"}"""));

        var result = properties.TryGetCertificateId(out var certificateId);

        Assert.True(result);
        Assert.Equal("abc123", certificateId);
    }

    [Fact]
    public void SetCertificateId_WithExistingMetadata_PreservesEndpointAndDnsProvider()
    {
        var tags = new Dictionary<string, string>
        {
            ["Acmebot"] = """{"endpoint":"acme-v02.api.letsencrypt.org","dnsProvider":"Azure DNS"}"""
        };

        tags.SetCertificateId("cert-id");

        var properties = CreateCertificateProperties(tags.Select(x => (x.Key, x.Value)).ToArray());

        Assert.True(properties.TryGetCertificateId(out var certificateId));
        Assert.Equal("cert-id", certificateId);
        Assert.True(properties.IsSameEndpoint(s_endpoint));
    }

    [Fact]
    public void ToCertificateTags_TrimsCustomTagsAndSkipsReservedAcmebotTag()
    {
        var policy = new CertificatePolicyItem
        {
            CertificateName = "example-com",
            DnsNames = ["example.com"],
            DnsProviderName = "Azure DNS",
            KeyType = "RSA",
            KeySize = 2048,
            Tags = new Dictionary<string, string>
            {
                [" environment "] = " production ",
                ["Acmebot"] = "user supplied"
            }
        };

        var tags = policy.ToCertificateTags(s_endpoint);

        Assert.Equal("production", tags["environment"]);
        Assert.Contains("Acmebot", tags.Keys);
        Assert.DoesNotContain("user supplied", tags.Values);
    }

    private static CertificateProperties CreateCertificateProperties(params (string Key, string Value)[] tags)
    {
        var properties = new CertificateProperties("example-com");

        foreach (var (key, value) in tags)
        {
            properties.Tags[key] = value;
        }

        return properties;
    }
}
