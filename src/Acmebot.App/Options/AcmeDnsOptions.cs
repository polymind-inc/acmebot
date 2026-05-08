namespace Acmebot.App.Options;

public class AcmeDnsOptions
{
    public int PropagationSeconds { get; set; } = 30;

    public required string Endpoint { get; set; }

    public required AcmeDnsZoneOptions[] Zones { get; set; }
}

public class AcmeDnsZoneOptions
{
    public required string Name { get; set; }

    public required string Subdomain { get; set; }

    public required string Username { get; set; }

    public required string Password { get; set; }
}
