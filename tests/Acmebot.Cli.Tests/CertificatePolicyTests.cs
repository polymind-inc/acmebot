using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class CertificatePolicyTests
{
    [Fact]
    public void Create_WithRsaDefaults_CreatesPolicy()
    {
        var policy = CertificatePolicyFactory.Create(CommandLine.Parse(
        [
            "certificate",
            "issue",
            "--dns-name",
            " example.com ",
            "--dns-name",
            "EXAMPLE.com",
            "--dns-provider",
            "Azure DNS",
            "--tag",
            "owner=platform"
        ]));

        Assert.Equal(["example.com"], policy.DnsNames);
        Assert.Equal("Azure DNS", policy.DnsProviderName);
        Assert.Equal("RSA", policy.KeyType);
        Assert.Equal(2048, policy.KeySize);
        Assert.Null(policy.KeyCurveName);
        Assert.NotNull(policy.Tags);
        Assert.Equal("platform", policy.Tags["owner"]);
    }

    [Fact]
    public void Create_WithEcDefaultsToP256()
    {
        var policy = CertificatePolicyFactory.Create(CommandLine.Parse(
        [
            "certificate",
            "issue",
            "--dns-name",
            "example.com",
            "--dns-provider",
            "Azure DNS",
            "--key-type",
            "EC"
        ]));

        Assert.Equal("EC", policy.KeyType);
        Assert.Null(policy.KeySize);
        Assert.Equal("P-256", policy.KeyCurveName);
    }

    [Fact]
    public void Create_WithLowercaseEcCurve_NormalizesCurveName()
    {
        var policy = CertificatePolicyFactory.Create(CommandLine.Parse(
        [
            "certificate",
            "issue",
            "--dns-name",
            "example.com",
            "--dns-provider",
            "Azure DNS",
            "--key-type",
            "ec",
            "--key-curve",
            "p-256k"
        ]));

        Assert.Equal("EC", policy.KeyType);
        Assert.Null(policy.KeySize);
        Assert.Equal("P-256K", policy.KeyCurveName);
    }

    [Fact]
    public void Create_WithInvalidCertificateName_Throws()
    {
        var ex = Assert.Throws<CliException>(() => CertificatePolicyFactory.Create(CommandLine.Parse(
        [
            "certificate",
            "issue",
            "--name",
            "bad_name",
            "--dns-name",
            "example.com",
            "--dns-provider",
            "Azure DNS"
        ])));

        Assert.Equal("Option '--name' must be 1 to 127 characters and contain only letters, numbers, and hyphens.", ex.Message);
    }

    [Fact]
    public void Create_WithTooLongCertificateName_Throws()
    {
        var ex = Assert.Throws<CliException>(() => CertificatePolicyFactory.Create(CommandLine.Parse(
        [
            "certificate",
            "issue",
            "--name",
            new string('a', 128),
            "--dns-name",
            "example.com",
            "--dns-provider",
            "Azure DNS"
        ])));

        Assert.Equal("Option '--name' must be 1 to 127 characters and contain only letters, numbers, and hyphens.", ex.Message);
    }

    [Fact]
    public void Create_WithReservedTag_Throws()
    {
        var ex = Assert.Throws<CliException>(() => CertificatePolicyFactory.Create(CommandLine.Parse(
        [
            "certificate",
            "issue",
            "--dns-name",
            "example.com",
            "--dns-provider",
            "Azure DNS",
            "--tag",
            "Acmebot=true"
        ])));

        Assert.Equal("The 'Acmebot' tag is reserved for internal metadata.", ex.Message);
    }

    [Fact]
    public void Create_WithoutDnsProvider_Throws()
    {
        var ex = Assert.Throws<CliException>(() => CertificatePolicyFactory.Create(CommandLine.Parse(
        [
            "certificate",
            "issue",
            "--dns-name",
            "example.com"
        ])));

        Assert.Equal("Option '--dns-provider' is required.", ex.Message);
    }
}
