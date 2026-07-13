using Acmebot.App.Notifications;
using Acmebot.App.Options;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class WebhookPayloadBuilderFactoryTests
{
    [Theory]
    [InlineData("https://hooks.slack.com/services/example", typeof(SlackPayloadBuilder))]
    [InlineData("https://prod-00.japaneast.logic.azure.com/workflows/example", typeof(TeamsPayloadBuilder))]
    [InlineData("https://example.environment.api.powerplatform.com/powerautomate/automations/direct/workflows/example", typeof(TeamsPayloadBuilder))]
    [InlineData("https://webhook.example/", typeof(GenericPayloadBuilder))]
    public void Create_WithAutomaticPayloadType_DetectsEndpoint(string endpoint, Type expectedType)
    {
        var builder = WebhookPayloadBuilderFactory.Create(CreateAcmebotOptions(), new WebhookOptions
        {
            Endpoint = new Uri(endpoint)
        });

        Assert.IsType(expectedType, builder);
    }

    [Fact]
    public void Create_WithExplicitGenericPayloadType_OverridesLogicAppsEndpoint()
    {
        var builder = WebhookPayloadBuilderFactory.Create(CreateAcmebotOptions(), new WebhookOptions
        {
            Endpoint = new Uri("https://prod-00.japaneast.logic.azure.com/workflows/example"),
            PayloadType = WebhookPayloadType.Generic
        });

        Assert.IsType<GenericPayloadBuilder>(builder);
    }

    [Fact]
    public void Create_WithExplicitTeamsPayloadType_OverridesEndpointDetection()
    {
        var builder = WebhookPayloadBuilderFactory.Create(CreateAcmebotOptions(), new WebhookOptions
        {
            Endpoint = new Uri("https://webhook.example/"),
            PayloadType = WebhookPayloadType.Teams
        });

        Assert.IsType<TeamsPayloadBuilder>(builder);
    }

    private static AcmebotOptions CreateAcmebotOptions()
    {
        return new AcmebotOptions
        {
            Contacts = "admin@example.com",
            Endpoint = new Uri("https://acme.example/directory"),
            VaultBaseUrl = "https://vault.example/"
        };
    }
}
