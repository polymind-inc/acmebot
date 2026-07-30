using System.Net.Http.Headers;

using Acmebot.App.Infrastructure;

namespace Acmebot.App.Providers;

/// <summary>
/// Creates the <see cref="HttpClient"/> instances that DNS providers use to talk to their REST APIs.
/// A User-Agent is required in practice: some provider edges (IONOS is one) reject requests without one
/// with a 5xx before authentication is even attempted, and the resulting failure surfaces only as an
/// empty zone list. Centralizing the defaults here keeps the header from being forgotten by a provider.
/// </summary>
internal static class DnsProviderHttpClient
{
    private static readonly ProductInfoHeaderValue s_userAgent = new("Acmebot", Constants.ApplicationVersion);

    public static HttpClient Create(string baseAddress, HttpMessageHandler? handler = null) => Create(new Uri(baseAddress), handler);

    public static HttpClient Create(Uri baseAddress, HttpMessageHandler? handler = null)
    {
        var httpClient = handler is null ? new HttpClient() : new HttpClient(handler);

        httpClient.BaseAddress = baseAddress;

        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        AddUserAgent(httpClient);

        return httpClient;
    }

    /// <summary>
    /// Applies the User-Agent to a client that was created elsewhere, such as by a provider SDK.
    /// </summary>
    public static void AddUserAgent(HttpClient httpClient) => httpClient.DefaultRequestHeaders.UserAgent.Add(s_userAgent);
}
