using System.Net;

using Acmebot.Acme.Models;

namespace Acmebot.Acme;

public sealed class AcmeProtocolException(
    HttpStatusCode statusCode,
    string message,
    Uri? requestUri = null,
    AcmeProblemDetails? problem = null,
    string? replayNonce = null,
    TimeSpan? retryAfter = null,
    IReadOnlyList<AcmeLinkHeader>? links = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public Uri? RequestUri { get; } = requestUri;

    public AcmeProblemDetails? Problem { get; } = problem;

    public string? ReplayNonce { get; } = replayNonce;

    public TimeSpan? RetryAfter { get; } = retryAfter;

    public IReadOnlyList<AcmeLinkHeader> Links { get; } = links ?? [];

    public bool IsBadNonce => Problem?.Type == AcmeProblemTypes.BadNonce;
}
