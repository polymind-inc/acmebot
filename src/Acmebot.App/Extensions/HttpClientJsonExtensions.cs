using System.Net.Http.Json;

namespace Acmebot.App.Extensions;

internal static class HttpClientJsonExtensions
{
    public static async Task<HttpResponseMessage> DeleteAsJsonAsync<T>(this HttpClient client, string requestUri, T value, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri)
        {
            Content = JsonContent.Create(value)
        };

        return await client.SendAsync(request, cancellationToken);
    }
}
