using System.Security.Claims;
using System.Text.Json;

using Acmebot.App.Functions.Http;
using Acmebot.App.Models;
using Acmebot.App.Options;
using Acmebot.App.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class GetConfigurationTests
{
    [Fact]
    public void HttpStart_WhenUnauthenticated_DoesNotReadConfiguration()
    {
        var context = new DefaultHttpContext();
        var function = new GetConfiguration(new HttpContextAccessor { HttpContext = context }, null!);

        Assert.IsType<UnauthorizedResult>(function.HttpStart(context.Request));
    }

    [Fact]
    public void HttpStart_WhenAuthenticated_ReturnsSnapshotWithoutAppRoleAndDisablesCaching()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "reader")], "test"))
        };
        var options = CreateOptions();
        options.RequireAppRoles = true;
        var function = new GetConfiguration(new HttpContextAccessor { HttpContext = context }, Microsoft.Extensions.Options.Options.Create(options));

        var result = Assert.IsType<OkObjectResult>(function.HttpStart(context.Request));
        var snapshot = Assert.IsType<ConfigurationItem>(result.Value);

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Empty(snapshot.DnsProviders);
        Assert.Contains(snapshot.Acme.Settings, x => x.Name == "Contact email" && x.Value == options.Contacts);
    }

    [Fact]
    public void HttpStart_MissingPowerDnsEndpoint_ReturnsRemainingConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Acmebot:Contacts"] = "admin@example.com",
            ["Acmebot:Endpoint"] = "https://ca.example/directory",
            ["Acmebot:VaultBaseUrl"] = "https://vault.example/",
            ["Acmebot:PowerDns:ApiKey"] = "powerdns-credential-marker",
            ["Acmebot:Cloudflare:ApiToken"] = "cloudflare-credential-marker"
        }).Build();
        var options = configuration.GetSection("Acmebot").Get<AcmebotOptions>()!;
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "reader")], "test"))
        };
        var function = new GetConfiguration(new HttpContextAccessor { HttpContext = context }, Microsoft.Extensions.Options.Options.Create(options));

        var result = Assert.IsType<OkObjectResult>(function.HttpStart(context.Request));
        var snapshot = Assert.IsType<ConfigurationItem>(result.Value);
        var powerDns = Assert.Single(snapshot.DnsProviders, x => x.Name == "PowerDNS");

        Assert.Null(Assert.Single(powerDns.Settings, x => x.Name == "Endpoint").Value);
        Assert.True(Assert.Single(powerDns.Settings, x => x.Name == "API key").IsConfigured);
        Assert.Contains(snapshot.DnsProviders, x => x.Name == "Cloudflare");
        Assert.Contains(snapshot.Acme.Settings, x => x.Value == "https://ca.example/directory");
        Assert.Contains(snapshot.General.Settings, x => x.Value == "https://vault.example/");
        Assert.DoesNotContain("credential-marker", JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void Snapshot_AllProviders_OmitsSecretValuesFromSerializedResponse()
    {
        const string secret = "never-send-this-credential";
        var options = CreateOptions();
        // Populate every provider so a missing credential projection cannot hide behind an untested provider.
        var providerProperties = typeof(AcmebotOptions).GetProperties()
            .Where(x => x.PropertyType.Namespace == typeof(CloudflareOptions).Namespace && x.Name != nameof(AcmebotOptions.ExternalAccountBinding));
        var secretNames = new HashSet<string>
        {
            "ClientToken", "ClientSecret", "AccessToken", "ApiToken", "ApiKey", "SecretKey",
            "ApiSecret", "KeyFile64", "ApplicationKey", "ApplicationSecret", "ConsumerKey", "AccessKey", "PrivateKeyName"
        };

        foreach (var property in providerProperties)
        {
            var provider = Activator.CreateInstance(property.PropertyType)!;
            foreach (var field in property.PropertyType.GetProperties())
            {
                if (field.PropertyType == typeof(string))
                {
                    field.SetValue(provider, secretNames.Contains(field.Name) ? secret : "example");
                }
                else if (field.PropertyType == typeof(Uri))
                {
                    field.SetValue(provider, new Uri("https://dns.example/"));
                }
            }
            property.SetValue(options, provider);
        }
        options.ExternalAccountBinding = new ExternalAccountBindingOptions { KeyId = secret, HmacKey = secret };

        var snapshot = ConfigurationSnapshot.Create(options);
        var json = JsonSerializer.Serialize(snapshot);

        Assert.Equal(16, snapshot.DnsProviders.Count);
        Assert.DoesNotContain(secret, json);
        var credentials = snapshot.DnsProviders.SelectMany(x => x.Settings).Concat(snapshot.Acme.Settings).Where(x => x.IsConfigured.HasValue).ToArray();
        Assert.NotEmpty(credentials);
        Assert.All(credentials, x =>
        {
            Assert.True(x.IsConfigured);
            Assert.Null(x.Value);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Snapshot_MissingCredential_IsReportedAsNotConfigured(string? token)
    {
        var options = CreateOptions();
        options.Cloudflare = new CloudflareOptions { ApiToken = token! };

        var setting = Assert.Single(Assert.Single(ConfigurationSnapshot.Create(options).DnsProviders).Settings);

        Assert.False(setting.IsConfigured);
        Assert.Null(setting.Value);
    }

    [Fact]
    public void Snapshot_Urls_OmitUserInfoQueryAndFragmentAndRejectInvalidUrls()
    {
        var options = CreateOptions();
        options.Endpoint = new Uri("https://user:password@ca.example/directory?token=url-credential-marker#url-credential-marker");
        options.VaultBaseUrl = "https://user:password@vault.example/?token=url-credential-marker#url-credential-marker";
        options.CustomDns = new CustomDnsOptions { Endpoint = "https://user:password@dns.example/api?token=url-credential-marker#url-credential-marker", ApiKey = "key" };
        options.Ovh = new OvhOptions { Endpoint = "invalid-secret", ApplicationKey = "", ApplicationSecret = "", ConsumerKey = "" };

        var snapshot = ConfigurationSnapshot.Create(options);
        var json = JsonSerializer.Serialize(snapshot);

        Assert.DoesNotContain("url-credential-marker", json);
        Assert.DoesNotContain("invalid-secret", json);
        Assert.DoesNotContain("password", json);
        Assert.Contains(snapshot.Acme.Settings, x => x.Value == "https://ca.example/directory");
        Assert.Contains(snapshot.DnsProviders[0].Settings, x => x.Value == "https://dns.example/api");
        Assert.Null(snapshot.DnsProviders[1].Settings[0].Value);
    }

    private static AcmebotOptions CreateOptions() => new()
    {
        Contacts = "admin@example.com",
        Endpoint = new Uri("https://ca.example/directory"),
        VaultBaseUrl = "https://vault.example/"
    };
}
