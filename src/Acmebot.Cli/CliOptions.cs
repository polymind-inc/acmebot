using System.Globalization;

namespace Acmebot.Cli;

internal sealed record CliOptions(
    Uri Endpoint,
    string[] TokenScopes,
    CredentialOptions CredentialOptions,
    OutputFormat OutputFormat,
    TimeSpan PollInterval,
    TimeSpan Timeout)
{
    public static CliOptions Create(CommandLine commandLine)
    {
        return Create(commandLine, CliConfig.Empty);
    }

    public static CliOptions Create(CommandLine commandLine, CliConfig config)
    {
        var endpointValue = GetOptionOrEnvironmentOrConfig(commandLine, "endpoint", "ACMEBOT_ENDPOINT", config.Endpoint);

        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            throw new CliException("Missing required option '--endpoint'. Run 'acmebot config set --endpoint <url>', set ACMEBOT_ENDPOINT, or pass --endpoint.");
        }

        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
        {
            throw new CliException("Option '--endpoint' must be an absolute URL.");
        }

        var credentialOptions = CredentialOptions.Create(commandLine);
        var tokenScopes = GetTokenScopes(commandLine, endpoint, config);
        var outputFormat = GetOutputFormat(commandLine);

        return new CliOptions(
            NormalizeEndpoint(endpoint),
            tokenScopes,
            credentialOptions,
            outputFormat,
            GetDuration(commandLine, "poll-interval", TimeSpan.FromSeconds(5)),
            GetDuration(commandLine, "timeout", TimeSpan.FromMinutes(30)));
    }

    private static Uri NormalizeEndpoint(Uri endpoint)
    {
        return new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
    }

    private static string[] GetTokenScopes(CommandLine commandLine, Uri endpoint, CliConfig config)
    {
        var audience = commandLine.GetOption("audience")
            ?? Environment.GetEnvironmentVariable("ACMEBOT_AUDIENCE")
            ?? config.Audience
            ?? endpoint.GetLeftPart(UriPartial.Authority);

        ValidateAudience(audience);

        return [ToDefaultScope(audience)];
    }

    public static void ValidateAudience(string audience)
    {
        if (audience.EndsWith("/.default", StringComparison.Ordinal) || audience.EndsWith("/user_impersonation", StringComparison.Ordinal))
        {
            throw new CliException("Option '--audience' must be an application ID URI or endpoint origin, not a token scope.");
        }
    }

    private static string ToDefaultScope(string audience)
    {
        return audience.TrimEnd('/') + "/.default";
    }

    private static OutputFormat GetOutputFormat(CommandLine commandLine)
    {
        if (commandLine.GetFlag("json"))
        {
            return OutputFormat.Json;
        }

        var value = commandLine.GetOption("format")
            ?? Environment.GetEnvironmentVariable("ACMEBOT_FORMAT")
            ?? "table";

        return value.ToLowerInvariant() switch
        {
            "json" => OutputFormat.Json,
            "table" => OutputFormat.Table,
            _ => throw new CliException("Option '--format' must be 'table' or 'json'.")
        };
    }

    private static TimeSpan GetDuration(CommandLine commandLine, string optionName, TimeSpan defaultValue)
    {
        var value = commandLine.GetOption(optionName);

        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
        {
            throw new CliException($"Option '--{optionName}' must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string? GetOptionOrEnvironmentOrConfig(CommandLine commandLine, string optionName, string environmentName, string? configValue)
    {
        return commandLine.GetOption(optionName) ?? Environment.GetEnvironmentVariable(environmentName) ?? configValue;
    }
}

internal enum OutputFormat
{
    Table,
    Json
}
