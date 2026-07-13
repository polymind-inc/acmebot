using Acmebot.App.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class WebhookOptionsTests
{
    [Fact]
    public void AddWebhookOptions_WithStructuredSettings_BindsValues()
    {
        var options = BindOptions(new Dictionary<string, string?>
        {
            ["Acmebot:Webhook:Endpoint"] = "https://webhook.example/",
            ["Acmebot:Webhook:Events"] = "Failed",
            ["Acmebot:Webhook:PayloadType"] = "Generic"
        });

        Assert.Equal(new Uri("https://webhook.example/"), options.Endpoint);
        Assert.Equal(WebhookEvents.Failed, options.Events);
        Assert.Equal(WebhookPayloadType.Generic, options.PayloadType);
    }

    [Fact]
    public void AddWebhookOptions_WithLegacyScalarSetting_UsesItAsEndpoint()
    {
        var options = BindOptions(new Dictionary<string, string?>
        {
            ["Acmebot:Webhook"] = "https://legacy-webhook.example/"
        });

        Assert.Equal(new Uri("https://legacy-webhook.example/"), options.Endpoint);
        Assert.Equal(WebhookEvents.All, options.Events);
        Assert.Equal(WebhookPayloadType.Auto, options.PayloadType);
    }

    [Fact]
    public void AddWebhookOptions_WithStructuredAndLegacySettings_PrefersStructuredEndpoint()
    {
        var options = BindOptions(new Dictionary<string, string?>
        {
            ["Acmebot:Webhook"] = "https://legacy-webhook.example/",
            ["Acmebot:Webhook:Endpoint"] = "https://webhook.example/"
        });

        Assert.Equal(new Uri("https://webhook.example/"), options.Endpoint);
    }

    [Fact]
    public void AddWebhookOptions_WithInvalidLegacyScalarSetting_FailsValidation()
    {
        Assert.Throws<OptionsValidationException>(() => BindOptions(new Dictionary<string, string?>
        {
            ["Acmebot:Webhook"] = "not-a-url"
        }));
    }

    private static WebhookOptions BindOptions(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();

        services.AddWebhookOptions(configuration.GetSection("Acmebot:Webhook"));

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<WebhookOptions>>().Value;
    }
}
