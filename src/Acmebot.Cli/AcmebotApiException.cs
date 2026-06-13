using System.Net;

namespace Acmebot.Cli;

internal sealed class AcmebotApiException(HttpStatusCode statusCode, string message, ProblemDetails? problemDetails = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public ProblemDetails? ProblemDetails { get; } = problemDetails;
}
