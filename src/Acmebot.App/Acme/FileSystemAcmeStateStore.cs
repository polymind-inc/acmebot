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

        // Write to a temporary file first and atomically replace the target so that a crash or host
        // recycle mid-write cannot leave a truncated state file (a corrupt account_key.json would
        // permanently orphan the ACME account).
        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, s_jsonSerializerOptions, cancellationToken);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);

            throw;
        }
    }

    private static void TryDeleteFile(string path)
    {
        // Best-effort cleanup of the temp file; never let a cleanup failure mask the original error.
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private string ResolveStateFullPath(string path) => Environment.ExpandEnvironmentVariables($"%HOME%/data/.acmebot/{_endpoint.Host}/{path}");
}
