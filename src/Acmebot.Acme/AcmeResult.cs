using Acmebot.Acme.Models;

namespace Acmebot.Acme;

public sealed class AcmeResult<T>
{
    public required T Resource { get; init; }

    public Uri? Location { get; init; }

    public TimeSpan? RetryAfter { get; init; }

    public IReadOnlyList<AcmeLinkHeader> Links { get; init; } = [];
}
