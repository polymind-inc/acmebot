using Microsoft.Extensions.Configuration;

namespace Acmebot.App.Infrastructure;

public sealed record AzureWebJobsStorageSettings(string? ConnectionString, string? BlobServiceUri, string? AccountName, string? ClientId)
{
    private const string ConnectionName = "AzureWebJobsStorage";

    public static AzureWebJobsStorageSettings FromConfiguration(IConfiguration configuration)
    {
        return new AzureWebJobsStorageSettings(
            configuration[ConnectionName],
            GetConnectionProperty(configuration, "blobServiceUri"),
            GetConnectionProperty(configuration, "accountName"),
            GetConnectionProperty(configuration, "clientId"));
    }

    public string GetBlobServiceUri(string storageEndpointSuffix)
    {
        if (!string.IsNullOrWhiteSpace(BlobServiceUri))
        {
            return BlobServiceUri;
        }

        if (!string.IsNullOrWhiteSpace(AccountName))
        {
            return $"https://{AccountName}.blob.{storageEndpointSuffix}";
        }

        throw new InvalidOperationException("AzureWebJobsStorage, AzureWebJobsStorage__blobServiceUri, or AzureWebJobsStorage__accountName is required.");
    }

    private static string? GetConnectionProperty(IConfiguration configuration, string name)
    {
        var value = configuration[$"{ConnectionName}:{name}"];

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return configuration[$"{ConnectionName}__{name}"];
    }
}
