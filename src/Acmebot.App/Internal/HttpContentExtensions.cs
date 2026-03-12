using System.Text.Json;

namespace Acmebot.App.Internal;

internal static class HttpContentExtensions
{
    public static async ValueTask<T> ReadAsAsync<T>(this HttpContent content)
    {
        await using var contentStream = await content.ReadAsStreamAsync();

        return await JsonSerializer.DeserializeAsync<T>(contentStream);
    }
}
