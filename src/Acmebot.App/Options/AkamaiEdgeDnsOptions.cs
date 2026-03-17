namespace Acmebot.App.Options;

public class AkamaiEdgeDnsOptions
{
    public required string Host { get; set; }

    public required string ClientToken { get; set; }

    public required string ClientSecret { get; set; }

    public required string AccessToken { get; set; }
}
