using System.Text.Json;

using Acmebot.App.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Acme;

internal sealed partial class FileSystemAcmeStateStore : IAcmeStateStore
{
    private readonly string _stateDirectory;
    private readonly ILogger<FileSystemAcmeStateStore> _logger;

    public FileSystemAcmeStateStore(IOptions<AcmebotOptions> options, ILogger<FileSystemAcmeStateStore> logger)
        : this(Environment.ExpandEnvironmentVariables($"%HOME%/data/.acmebot/{options.Value.Endpoint.Host}"), logger)
    {
    }

    internal FileSystemAcmeStateStore(string stateDirectory, ILogger<FileSystemAcmeStateStore>? logger = null)
    {
        _stateDirectory = Path.GetFullPath(stateDirectory);
        _logger = logger ?? NullLogger<FileSystemAcmeStateStore>.Instance;
    }

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_stateDirectory);
        var lockPath = Path.Join(_stateDirectory, "account.lock");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                return new FileSystemAcmeStateStoreLock(stream, cancellationToken);
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 11 or 32 or 33)
            {
                await Task.Delay(Random.Shared.Next(250, 750), cancellationToken);
            }
        }
    }

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

    public async Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveCoreAsync(value, path, overwrite: false, cancellationToken);

            return true;
        }
        catch (IOException ex) when ((ex.HResult & 0xffff) is 17 or 80 or 183)
        {
            return false;
        }
    }

    public Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => SaveCoreAsync(value, path, overwrite: true, cancellationToken);

    private async Task SaveCoreAsync<TState>(TState value, string path, bool overwrite, CancellationToken cancellationToken)
    {
        var fullPath = ResolveStateFullPath(path);
        Directory.CreateDirectory(_stateDirectory);

        // Write to a temporary file first and atomically replace the target so that a crash or host
        // recycle mid-write cannot leave a truncated state file (a corrupt account_key.json would
        // permanently orphan the ACME account).
        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        try
        {
            await using (stream)
            {
                await JsonSerializer.SerializeAsync(stream, value, s_jsonSerializerOptions, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, fullPath, overwrite);
        }
        catch
        {
            TryDeleteFile(tempPath);

            throw;
        }
    }

    private void TryDeleteFile(string path)
    {
        // Best-effort cleanup of the temp file; never let a cleanup failure mask the original error.
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogTemporaryFileCleanupFailed(_logger, ex, path);
        }
    }

    private string ResolveStateFullPath(string path)
    {
        if (path is not ("account.json" or "account_key.json"))
        {
            throw new ArgumentException("The state file must be account.json or account_key.json.", nameof(path));
        }

        return Path.Join(_stateDirectory, path);
    }

    [LoggerMessage(LogLevel.Warning, "Could not remove the temporary ACME state file {Path}.")]
    private static partial void LogTemporaryFileCleanupFailed(ILogger logger, Exception exception, string path);

    private sealed class FileSystemAcmeStateStoreLock(FileStream stream, CancellationToken cancellationToken) : IAcmeStateStoreLock
    {
        public CancellationToken CancellationToken => cancellationToken;

        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }
}
