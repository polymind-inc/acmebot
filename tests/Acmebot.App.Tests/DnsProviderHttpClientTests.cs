using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsProviderHttpClientTests
{
    [Fact]
    public void Create_SetsBaseAddressAndJsonAccept()
    {
        using var httpClient = DnsProviderHttpClient.Create("https://api.example.com/v1/");

        Assert.Equal(new Uri("https://api.example.com/v1/"), httpClient.BaseAddress);
        Assert.Contains(httpClient.DefaultRequestHeaders.Accept, x => x.MediaType == "application/json");
    }

    [Fact]
    public void Create_SendsAcmebotUserAgent()
    {
        using var httpClient = DnsProviderHttpClient.Create("https://api.example.com/v1/");

        var userAgent = Assert.Single(httpClient.DefaultRequestHeaders.UserAgent);

        Assert.Equal("Acmebot", userAgent.Product?.Name);
        Assert.False(string.IsNullOrEmpty(userAgent.Product?.Version));
    }

    [Fact]
    public void Create_WithHandler_SendsAcmebotUserAgent()
    {
        using var httpClient = DnsProviderHttpClient.Create("https://api.example.com/v1/", new HttpClientHandler());

        Assert.Contains(httpClient.DefaultRequestHeaders.UserAgent, x => x.Product?.Name == "Acmebot");
    }

    [Fact]
    public void AddUserAgent_AppliesToClientCreatedElsewhere()
    {
        using var httpClient = new HttpClient();

        DnsProviderHttpClient.AddUserAgent(httpClient);

        Assert.Contains(httpClient.DefaultRequestHeaders.UserAgent, x => x.Product?.Name == "Acmebot");
    }
}
