namespace Acmebot.App.Options;

public class OvhOptions
{
    public string Endpoint { get; set; } = "https://eu.api.ovh.com/v2/";

    public required string ApplicationKey { get; set; }

    public required string ApplicationSecret { get; set; }

    public required string ConsumerKey { get; set; }
}
