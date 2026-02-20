namespace Acmebot.Options;

public class CustomDnsOptions
{
    public int PropagationSeconds { get; set; } = 180;

    public required string Endpoint { get; set; }

    public required string ApiKey { get; set; }

    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
}
