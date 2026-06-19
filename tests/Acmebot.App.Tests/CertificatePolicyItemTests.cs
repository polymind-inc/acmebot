using System.ComponentModel.DataAnnotations;

using Acmebot.App.Models;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificatePolicyItemTests
{
    [Fact]
    public void Validate_WithEmptyDnsNames_ReturnsError()
    {
        var policy = CreatePolicy(dnsNames: []);

        var result = Assert.Single(Validate(policy));

        Assert.Equal("The DnsNames is required.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithMissingDnsProvider_ReturnsError()
    {
        var policy = CreatePolicy(dnsProviderName: "");

        var result = Assert.Single(Validate(policy));

        Assert.Equal("The DnsProviderName is required.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithMissingCertificateName_ReturnsError()
    {
        var policy = CreatePolicy(certificateName: "");

        var result = Assert.Single(Validate(policy));

        Assert.Equal("The CertificateName is required.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithInvalidCertificateName_ReturnsError()
    {
        var policy = CreatePolicy(certificateName: "bad_name");

        var result = Assert.Single(Validate(policy));

        Assert.Equal("The CertificateName must be 1 to 127 characters and contain only letters, numbers, and hyphens.", result.ErrorMessage);
    }

    [Fact]
    public void AliasedDnsNames_WithDnsAlias_ReturnsAliasOnly()
    {
        var policy = CreatePolicy(dnsNames: ["mail.fabrikam.com"], dnsAlias: "mail-fabrikam-com.acme.example.com");

        Assert.Equal(["mail-fabrikam-com.acme.example.com"], policy.AliasedDnsNames);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("www.example.com")]
    [InlineData("*.example.com")]
    [InlineData("WWW.Example.COM")]
    [InlineData("xn--caf-dma.example.jp")]
    public void Validate_WithValidDnsName_ReturnsNoError(string dnsName)
    {
        var policy = CreatePolicy(dnsNames: [dnsName]);

        Assert.Empty(Validate(policy));
    }

    [Theory]
    [InlineData("www", "The DnsNames must include a domain suffix.")]
    [InlineData("a..b", "The DnsNames cannot contain empty DNS labels.")]
    [InlineData("example.com.", "The DnsNames cannot contain empty DNS labels.")]
    [InlineData("sub.*.example.com", "A wildcard can only be the leftmost DNS label.")]
    [InlineData("*foo.example.com", "The DnsNames must be an ASCII DNS name containing only letters, numbers, hyphens, dots, and a leftmost wildcard.")]
    [InlineData("under_score.example.com", "The DnsNames must be an ASCII DNS name containing only letters, numbers, hyphens, dots, and a leftmost wildcard.")]
    [InlineData("café.example.jp", "The DnsNames must be an ASCII DNS name containing only letters, numbers, hyphens, dots, and a leftmost wildcard.")]
    [InlineData(" example.com", "The DnsNames must be an ASCII DNS name containing only letters, numbers, hyphens, dots, and a leftmost wildcard.")]
    public void Validate_WithInvalidDnsName_ReturnsError(string dnsName, string expectedMessage)
    {
        var policy = CreatePolicy(dnsNames: [dnsName]);

        var result = Assert.Single(Validate(policy));

        Assert.Equal(expectedMessage, result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithNullDnsName_ReturnsError()
    {
        var policy = CreatePolicy(dnsNames: [null!]);

        var result = Assert.Single(Validate(policy));

        Assert.Equal("The DnsNames contains an empty DNS name.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithWildcardDnsAlias_ReturnsError()
    {
        var policy = CreatePolicy(dnsAlias: "*.acme.example.com");

        var result = Assert.Single(Validate(policy));

        Assert.Equal("The DnsAlias cannot be a wildcard.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithChallengePrefixedDnsAlias_ReturnsError()
    {
        var policy = CreatePolicy(dnsAlias: "_acme-challenge.acme.example.com");

        var result = Assert.Single(Validate(policy));

        Assert.Equal("The DnsAlias must be an ASCII DNS name containing only letters, numbers, hyphens, and dots.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithValidDnsAlias_ReturnsNoError()
    {
        var policy = CreatePolicy(dnsAlias: "mail-fabrikam-com.acme.example.com");

        Assert.Empty(Validate(policy));
    }

    private static CertificatePolicyItem CreatePolicy(
        string certificateName = "example-com",
        string[]? dnsNames = null,
        string dnsProviderName = "Azure DNS",
        string? dnsAlias = null)
    {
        return new CertificatePolicyItem
        {
            CertificateName = certificateName,
            DnsNames = dnsNames ?? ["example.com"],
            DnsProviderName = dnsProviderName,
            KeyType = "RSA",
            KeySize = 2048,
            DnsAlias = dnsAlias
        };
    }

    private static IReadOnlyList<ValidationResult> Validate(CertificatePolicyItem policy)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(policy, new ValidationContext(policy), results, validateAllProperties: true);
        return results;
    }
}
