using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acmebot.Cli;

internal sealed record CliConfig(string? Endpoint, string? Audience)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static CliConfig Empty { get; } = new(null, null);

    public static CliConfig Load(CommandLine commandLine)
    {
        var path = TryGetPath(commandLine);

        if (path is null || !File.Exists(path))
        {
            return Empty;
        }

        try
        {
            var config = JsonSerializer.Deserialize<CliConfigFile>(File.ReadAllText(path), s_jsonOptions);

            return new CliConfig(Normalize(config?.Endpoint), Normalize(config?.Audience));
        }
        catch (JsonException ex)
        {
            throw new CliException($"CLI configuration file '{path}' is not valid JSON: {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new CliException($"Could not read CLI configuration file '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new CliException($"Could not read CLI configuration file '{path}': {ex.Message}");
        }
    }

    public void Save(CommandLine commandLine)
    {
        var path = GetPath(commandLine);
        var directory = Path.GetDirectoryName(path);

        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var config = new CliConfigFile
            {
                Endpoint = Normalize(Endpoint),
                Audience = Normalize(Audience)
            };

            File.WriteAllText(path, JsonSerializer.Serialize(config, s_jsonOptions) + Environment.NewLine);
        }
        catch (IOException ex)
        {
            throw new CliException($"Could not write CLI configuration file '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new CliException($"Could not write CLI configuration file '{path}': {ex.Message}");
        }
    }

    public static void Delete(CommandLine commandLine)
    {
        var path = GetPath(commandLine);

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            throw new CliException($"Could not delete CLI configuration file '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new CliException($"Could not delete CLI configuration file '{path}': {ex.Message}");
        }
    }

    public static string GetPath(CommandLine commandLine)
    {
        return TryGetPath(commandLine)
            ?? throw new CliException("Cannot determine CLI configuration path. Set ACMEBOT_CONFIG or pass --config.");
    }

    private static string? TryGetPath(CommandLine commandLine)
    {
        var explicitPath = commandLine.GetOption("config") ?? Environment.GetEnvironmentVariable("ACMEBOT_CONFIG");

        if (explicitPath is not null)
        {
            if (string.IsNullOrWhiteSpace(explicitPath))
            {
                throw new CliException("Option '--config' must not be empty.");
            }

            return Path.GetFullPath(explicitPath);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return string.IsNullOrWhiteSpace(userProfile)
            ? null
            : Path.Combine(userProfile, ".acmebot", "config.json");
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class CliConfigFile
    {
        public string? Endpoint { get; set; }

        public string? Audience { get; set; }
    }
}
