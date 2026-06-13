using Azure.Core;
using Azure.Identity;

namespace Acmebot.Cli;

internal sealed record CredentialOptions(
    string? TenantId,
    string? ClientId,
    string? ClientSecret,
    string? ClientCertificatePath,
    string? ClientCertificatePassword,
    string? ManagedIdentityClientId)
{
    public static CredentialOptions Create(CommandLine commandLine)
    {
        var tenantId = commandLine.GetOption("tenant-id") ?? Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var clientId = commandLine.GetOption("client-id") ?? Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var clientSecret = commandLine.GetOption("client-secret");
        var clientCertificatePath = commandLine.GetOption("client-certificate-path");
        var clientCertificatePasswordOption = commandLine.GetOption("client-certificate-password");
        var clientCertificatePassword = clientCertificatePasswordOption
            ?? Environment.GetEnvironmentVariable("AZURE_CLIENT_CERTIFICATE_PASSWORD");

        if (!string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(clientCertificatePath))
        {
            throw new CliException("Specify either '--client-secret' or '--client-certificate-path', not both.");
        }

        if (string.IsNullOrWhiteSpace(clientSecret) && string.IsNullOrWhiteSpace(clientCertificatePath))
        {
            clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                clientCertificatePath = Environment.GetEnvironmentVariable("AZURE_CLIENT_CERTIFICATE_PATH");
            }
        }

        if (!string.IsNullOrWhiteSpace(clientCertificatePassword) &&
            string.IsNullOrWhiteSpace(clientCertificatePath) &&
            (!string.IsNullOrWhiteSpace(clientCertificatePasswordOption) || string.IsNullOrWhiteSpace(clientSecret)))
        {
            throw new CliException("Option '--client-certificate-password' requires '--client-certificate-path'.");
        }

        if (string.IsNullOrWhiteSpace(clientCertificatePath))
        {
            clientCertificatePassword = null;
        }

        return new CredentialOptions(
            tenantId,
            clientId,
            clientSecret,
            clientCertificatePath,
            clientCertificatePassword,
            commandLine.GetOption("managed-identity-client-id") ?? Environment.GetEnvironmentVariable("ACMEBOT_MANAGED_IDENTITY_CLIENT_ID"));
    }

    public TokenCredential CreateCredential()
    {
        if (!string.IsNullOrWhiteSpace(ClientSecret))
        {
            return new ClientSecretCredential(RequireTenantId(), RequireClientId(), ClientSecret);
        }

        if (!string.IsNullOrWhiteSpace(ClientCertificatePath))
        {
            if (!string.IsNullOrEmpty(ClientCertificatePassword))
            {
                var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(ClientCertificatePath, ClientCertificatePassword);

                return new ClientCertificateCredential(RequireTenantId(), RequireClientId(), certificate);
            }

            return new ClientCertificateCredential(RequireTenantId(), RequireClientId(), ClientCertificatePath);
        }

        var options = new DefaultAzureCredentialOptions();

        if (!string.IsNullOrWhiteSpace(TenantId))
        {
            options.TenantId = TenantId;
        }

        if (!string.IsNullOrWhiteSpace(ManagedIdentityClientId))
        {
            options.ManagedIdentityClientId = ManagedIdentityClientId;
        }

        return new DefaultAzureCredential(options);
    }

    private string RequireTenantId()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
        {
            throw new CliException("Options '--tenant-id' and '--client-id' are required for explicit service principal authentication.");
        }

        return TenantId;
    }

    private string RequireClientId()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new CliException("Options '--tenant-id' and '--client-id' are required for explicit service principal authentication.");
        }

        return ClientId;
    }
}
