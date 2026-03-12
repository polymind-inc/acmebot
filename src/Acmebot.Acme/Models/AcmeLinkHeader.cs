namespace Acmebot.Acme.Models;

public sealed record AcmeLinkHeader
{
    public required Uri Uri { get; init; }

    public string? Relation { get; init; }

    public string? MediaType { get; init; }

    public string? Title { get; init; }
}
