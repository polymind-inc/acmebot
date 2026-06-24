using System.Text.Json;

using Acmebot.App.Extensions;
using Acmebot.App.Models;

using Azure.Security.KeyVault.Certificates;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateExtensionsTests
{
    [Fact]
    public void ToCertificateTags_WithProfile_StoresProfileInMetadata()
    {
        var policy = CreatePolicy(profile: " tlsserver ");

        var tags = policy.ToCertificateTags(new Uri("https://acme.example/directory"));

        using var metadata = JsonDocument.Parse(tags["Acmebot"]);
        Assert.Equal("tlsserver", metadata.RootElement.GetProperty("profile").GetString());
    }

    [Fact]
    public void ToCertificateTags_WithoutProfile_OmitsProfileFromMetadata()
    {
        var policy = CreatePolicy(profile: null);

        var tags = policy.ToCertificateTags(new Uri("https://acme.example/directory"));

        using var metadata = JsonDocument.Parse(tags["Acmebot"]);
        Assert.False(metadata.RootElement.TryGetProperty("profile", out _));
    }

    [Fact]
    public void ToCertificatePolicyItem_WithProfileMetadata_RestoresProfile()
    {
        var tags = CreatePolicy(profile: "tlsserver").ToCertificateTags(new Uri("https://acme.example/directory"));
        var certificate = CreateCertificate(tags);

        var policy = certificate.ToCertificatePolicyItem();

        Assert.Equal("tlsserver", policy.Profile);
    }

    [Fact]
    public void SetCertificateId_PreservesProfileMetadata()
    {
        var tags = CreatePolicy(profile: "tlsserver").ToCertificateTags(new Uri("https://acme.example/directory"));

        tags.SetCertificateId("certificate-id");

        using var metadata = JsonDocument.Parse(tags["Acmebot"]);
        Assert.Equal("tlsserver", metadata.RootElement.GetProperty("profile").GetString());
        Assert.Equal("certificate-id", metadata.RootElement.GetProperty("certificateId").GetString());
    }

    private static CertificatePolicyItem CreatePolicy(string? profile)
    {
        return new CertificatePolicyItem
        {
            CertificateName = "example-com",
            DnsNames = ["example.com"],
            DnsProviderName = "Azure DNS",
            KeyType = "RSA",
            KeySize = 2048,
            Profile = profile
        };
    }

    private static KeyVaultCertificateWithPolicy CreateCertificate(IDictionary<string, string> tags)
    {
        var subjectAlternativeNames = new SubjectAlternativeNames();
        subjectAlternativeNames.DnsNames.Add("example.com");

        var policy = new CertificatePolicy(WellKnownIssuerNames.Unknown, "CN=example.com", subjectAlternativeNames)
        {
            KeyType = CertificateKeyType.Rsa,
            KeySize = 2048
        };

        var properties = new CertificateProperties("example-com");

        foreach (var tag in tags)
        {
            properties.Tags[tag.Key] = tag.Value;
        }

        return CertificateModelFactory.KeyVaultCertificateWithPolicy(
            properties,
            new Uri("https://vault.example/keys/example-com/version"),
            new Uri("https://vault.example/secrets/example-com/version"),
            [],
            policy);
    }
}
