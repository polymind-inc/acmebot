using System.Text.Json;

using Acmebot.App.Options;

using Microsoft.Extensions.Options;

namespace Acmebot.App.Acme;

internal sealed class FileSystemAcmeStateStore(IOptions<AcmebotOptions> options) : IAcmeStateStore
{
    private readonly Uri _endpoint = options.Value.Endpoint;

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveStateFullPath(path);

        if (!File.Exists(fullPath))
        {
            return default;
        }

        await using var stream = File.OpenRead(fullPath);

        return await JsonSerializer.DeserializeAsync<TState>(stream, s_jsonSerializerOptions, cancellationToken);
    }

    public async Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveStateFullPath(path);
        var directoryPath = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var stream = File.Create(fullPath);

        await JsonSerializer.SerializeAsync(stream, value, s_jsonSerializerOptions, cancellationToken);
    }

    private string ResolveStateFullPath(string path) => Environment.ExpandEnvironmentVariables($"%HOME%/data/.acmebot/{_endpoint.Host}/{path}");
}
