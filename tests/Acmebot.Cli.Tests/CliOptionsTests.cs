using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void Create_WithEndpoint_UsesEndpointOriginAsDefaultAudience()
    {
        var options = CliOptions.Create(CommandLine.Parse(["--endpoint", "https://acmebot.example/path", "certificate", "list"]));

        Assert.Equal(new Uri("https://acmebot.example/path/"), options.Endpoint);
        Assert.Equal(["https://acmebot.example/.default"], options.TokenScopes);
    }

    [Fact]
    public void Create_WithAudience_AppendsDefaultScope()
    {
        var options = CliOptions.Create(CommandLine.Parse(
        [
            "--endpoint",
            "https://acmebot.example",
            "--audience",
            "api://f3b48385-9523-470f-9f85-6ed488a1f6f2",
            "certificate",
            "list"
        ]));

        Assert.Equal(["api://f3b48385-9523-470f-9f85-6ed488a1f6f2/.default"], options.TokenScopes);
    }

    [Fact]
    public void Create_WithScopeAsAudience_Throws()
    {
        var ex = Assert.Throws<CliException>(() => CliOptions.Create(CommandLine.Parse(
        [
            "--endpoint",
            "https://acmebot.example",
            "--audience",
            "api://f3b48385-9523-470f-9f85-6ed488a1f6f2/user_impersonation",
            "certificate",
            "list"
        ])));

        Assert.Equal("Option '--audience' must be an application ID URI or endpoint origin, not a token scope.", ex.Message);
    }

    [Fact]
    public void Create_WithCertificatePasswordButNoCertificatePath_Throws()
    {
        var ex = Assert.Throws<CliException>(() => CliOptions.Create(CommandLine.Parse(
        [
            "--endpoint",
            "https://acmebot.example",
            "--client-certificate-password",
            "secret",
            "certificate",
            "list"
        ])));

        Assert.Equal("Option '--client-certificate-password' requires '--client-certificate-path'.", ex.Message);
    }
}
