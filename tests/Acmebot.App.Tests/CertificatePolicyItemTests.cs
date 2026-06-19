using System.ComponentModel.DataAnnotations;

using Acmebot.App.Models;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificatePolicyItemTests
{
    [Fact]
    public void Validate_WithDelegatedDnsAlias_Succeeds()
    {
        var policy = CreatePolicy(dnsNames: ["mail.fabrikam.com"], dnsAlias: "mail-fabrikam-com.acme.example.com");

        var results = Validate(policy);

        Assert.Empty(results);
    }

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
    public void AliasedDnsNames_WithDnsAlias_ReturnsAliasOnly()
    {
        var policy = CreatePolicy(dnsNames: ["mail.fabrikam.com"], dnsAlias: "mail-fabrikam-com.acme.example.com");

        Assert.Equal(["mail-fabrikam-com.acme.example.com"], policy.AliasedDnsNames);
    }

    private static CertificatePolicyItem CreatePolicy(
        string[]? dnsNames = null,
        string dnsProviderName = "Azure DNS",
        string? dnsAlias = null)
    {
        return new CertificatePolicyItem
        {
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
