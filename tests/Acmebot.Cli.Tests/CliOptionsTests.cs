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
        using var environment = EnvironmentVariableScope.Set(
        [
            ("AZURE_CLIENT_SECRET", null),
            ("AZURE_CLIENT_CERTIFICATE_PATH", null),
            ("AZURE_CLIENT_CERTIFICATE_PASSWORD", null)
        ]);

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

    [Fact]
    public void Create_WithAzureIdentityClientSecretEnvironmentVariables_UsesClientSecret()
    {
        using var environment = EnvironmentVariableScope.Set(
        [
            ("AZURE_TENANT_ID", "tenant"),
            ("AZURE_CLIENT_ID", "client"),
            ("AZURE_CLIENT_SECRET", "secret"),
            ("AZURE_CLIENT_CERTIFICATE_PATH", null),
            ("AZURE_CLIENT_CERTIFICATE_PASSWORD", "unused-password")
        ]);

        var options = CliOptions.Create(CommandLine.Parse(["--endpoint", "https://acmebot.example", "certificate", "list"]));

        Assert.Equal("tenant", options.CredentialOptions.TenantId);
        Assert.Equal("client", options.CredentialOptions.ClientId);
        Assert.Equal("secret", options.CredentialOptions.ClientSecret);
        Assert.Null(options.CredentialOptions.ClientCertificatePath);
        Assert.Null(options.CredentialOptions.ClientCertificatePassword);
    }

    [Fact]
    public void Create_WithAzureIdentityCertificateEnvironmentVariables_UsesClientCertificate()
    {
        using var environment = EnvironmentVariableScope.Set(
        [
            ("AZURE_TENANT_ID", "tenant"),
            ("AZURE_CLIENT_ID", "client"),
            ("AZURE_CLIENT_SECRET", null),
            ("AZURE_CLIENT_CERTIFICATE_PATH", "certificate.pfx"),
            ("AZURE_CLIENT_CERTIFICATE_PASSWORD", "password")
        ]);

        var options = CliOptions.Create(CommandLine.Parse(["--endpoint", "https://acmebot.example", "certificate", "list"]));

        Assert.Equal("tenant", options.CredentialOptions.TenantId);
        Assert.Equal("client", options.CredentialOptions.ClientId);
        Assert.Null(options.CredentialOptions.ClientSecret);
        Assert.Equal("certificate.pfx", options.CredentialOptions.ClientCertificatePath);
        Assert.Equal("password", options.CredentialOptions.ClientCertificatePassword);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previousValues = [];

        private EnvironmentVariableScope(IReadOnlyList<(string Name, string? Value)> variables)
        {
            foreach (var (name, value) in variables)
            {
                _previousValues[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public static EnvironmentVariableScope Set(IReadOnlyList<(string Name, string? Value)> variables)
        {
            return new EnvironmentVariableScope(variables);
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previousValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
