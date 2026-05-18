namespace Acmebot.App.Options;

public class PowerDnsOptions
{
    public required Uri Endpoint { get; set; }

    public required string ApiKey { get; set; }

    public string ServerId { get; set; } = "localhost";
}
