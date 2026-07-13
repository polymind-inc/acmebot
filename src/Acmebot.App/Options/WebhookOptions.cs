using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmebot.App.Options;

public class WebhookOptions
{
    public Uri? Endpoint { get; set; }

    public WebhookEvents Events { get; set; } = WebhookEvents.All;

    public WebhookPayloadType PayloadType { get; set; } = WebhookPayloadType.Auto;
}

[Flags]
public enum WebhookEvents
{
    None = 0,
    Completed = 1,
    Failed = 2,
    All = Completed | Failed
}

public enum WebhookPayloadType
{
    Auto,
    Generic,
    Teams,
    Slack
}

internal static class WebhookOptionsExtensions
{
    public static IServiceCollection AddWebhookOptions(this IServiceCollection services, IConfigurationSection section)
    {
        services.AddOptions<WebhookOptions>()
                .Bind(section)
                .PostConfigure(options =>
                {
                    // Acmebot__Webhook was a scalar URL before structured webhook options were introduced.
                    if (options.Endpoint is null && Uri.TryCreate(section.Value, UriKind.Absolute, out var endpoint))
                    {
                        options.Endpoint = endpoint;
                    }
                })
                .Validate(options => options.Endpoint is not null || string.IsNullOrWhiteSpace(section.Value), "Legacy webhook endpoint must be an absolute URI.")
                .Validate(options => options.Endpoint is null || options.Endpoint.IsAbsoluteUri, "Webhook endpoint must be an absolute URI.")
                .Validate(options => Enum.IsDefined(options.PayloadType), "Webhook payload type is invalid.")
                .Validate(options => (options.Events & ~WebhookEvents.All) == 0, "Webhook events contain an invalid value.");

        return services;
    }
}
