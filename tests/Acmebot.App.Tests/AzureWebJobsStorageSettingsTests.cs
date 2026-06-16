using Acmebot.App.Infrastructure;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class AzureWebJobsStorageSettingsTests
{
    [Fact]
    public void FromConfiguration_WithConnectionString_ReadsConnectionString()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true"
        });

        var settings = AzureWebJobsStorageSettings.FromConfiguration(configuration);

        Assert.Equal("UseDevelopmentStorage=true", settings.ConnectionString);
    }

    [Fact]
    public void FromConfiguration_WithDoubleUnderscoreKeys_ReadsIdentityBasedConnectionProperties()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AzureWebJobsStorage__accountName"] = "storageaccount",
            ["AzureWebJobsStorage__blobServiceUri"] = "https://custom.blob.core.windows.net",
            ["AzureWebJobsStorage__clientId"] = "11111111-1111-1111-1111-111111111111"
        });

        var settings = AzureWebJobsStorageSettings.FromConfiguration(configuration);

        Assert.Equal("storageaccount", settings.AccountName);
        Assert.Equal("https://custom.blob.core.windows.net", settings.GetBlobServiceUri());
        Assert.Equal("11111111-1111-1111-1111-111111111111", settings.ClientId);
    }

    [Fact]
    public void GetBlobServiceUri_WithAccountName_BuildsDefaultPublicAzureUri()
    {
        var settings = new AzureWebJobsStorageSettings(null, null, "storageaccount", null);

        Assert.Equal("https://storageaccount.blob.core.windows.net", settings.GetBlobServiceUri());
    }

    [Fact]
    public void GetBlobServiceUri_WithoutBlobServiceUriOrAccountName_Throws()
    {
        var settings = new AzureWebJobsStorageSettings(null, null, null, null);

        var ex = Assert.Throws<InvalidOperationException>(settings.GetBlobServiceUri);

        Assert.Equal("AzureWebJobsStorage, AzureWebJobsStorage__blobServiceUri, or AzureWebJobsStorage__accountName is required.", ex.Message);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
               .AddInMemoryCollection(values)
               .Build();
    }
}
