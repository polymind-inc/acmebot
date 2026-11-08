using System.Security.Claims;

using Acmebot.App.Extensions;
using Acmebot.App.Functions.Http;
using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Infrastructure;
using Acmebot.App.Models;
using Acmebot.App.Options;
using Acmebot.App.Providers;
using Acmebot.App.Services;

using Azure.Core;

using DnsClient;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Acmebot.App.Tests;

[Collection(AppServiceAuthenticationCollection.Name)]
public sealed class DnsProviderRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddDnsProviders_ResolvesOnlyConfiguredProviders(bool configureProvider)
    {
        using var services = CreateServices(configureProvider);

        var providers = services.GetRequiredService<IEnumerable<IDnsProvider>>();

        if (configureProvider)
        {
            Assert.IsType<CustomDnsProvider>(Assert.Single(providers));
        }
        else
        {
            Assert.Empty(providers);
        }
    }

    [Fact]
    public async Task GetDnsZones_WithoutConfiguredProviders_ReturnsOkWithEmptyList()
    {
        using var authentication = new AppServiceAuthenticationScope("true");
        using var services = CreateServices();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin@example.com")], "aad")),
            RequestServices = services
        };
        var function = ActivatorUtilities.CreateInstance<GetDnsZones>(services, new HttpContextAccessor { HttpContext = context });

        var result = await function.HttpStart(context.Request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<DnsZoneGroup>>(ok.Value));
    }

    [Fact]
    public async Task Dns01Precondition_WithoutConfiguredProviders_ReportsMissingProvider()
    {
        using var services = CreateServices();
        var activities = new DnsChallengeActivities(
            new LookupClient(), null!, services.GetRequiredService<DnsZoneQueryService>(), NullLogger<DnsChallengeActivities>.Instance);
        var policy = new CertificatePolicyItem
        {
            CertificateName = "example-com",
            DnsNames = ["example.com"],
            DnsProviderName = "Azure DNS",
            KeyType = "RSA",
            KeySize = 2048
        };

        var exception = await Assert.ThrowsAsync<PreconditionException>(() => activities.Dns01Precondition(policy));

        Assert.Contains("'Azure DNS' is not configured", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Configured DNS providers: (none)", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider CreateServices(bool configureProvider = false)
    {
        var options = new AcmebotOptions
        {
            Contacts = "admin@example.com",
            Endpoint = new Uri("https://acme.example/directory"),
            VaultBaseUrl = "https://vault.example/",
            CustomDns = configureProvider ? new CustomDnsOptions { Endpoint = "https://dns.example/", ApiKey = "test-key" } : null
        };

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IOptions<AcmebotOptions>>(Microsoft.Extensions.Options.Options.Create(options))
            .AddSingleton(AzureEnvironment.Get("AzureCloud"))
            .AddSingleton<TokenCredential>(new ThrowingCredential())
            .AddDnsProviders()
            .AddSingleton<DnsZoneQueryService>()
            .BuildServiceProvider();
    }

    private sealed class ThrowingCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Provider registration must not request an access token.");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Provider registration must not request an access token.");
    }
}
