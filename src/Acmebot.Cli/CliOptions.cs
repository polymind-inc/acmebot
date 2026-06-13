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
        var endpointValue = GetOptionOrEnvironment(commandLine, "endpoint", "ACMEBOT_ENDPOINT");

        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            throw new CliException("Missing required option '--endpoint'. You can also set ACMEBOT_ENDPOINT.");
        }

        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
        {
            throw new CliException("Option '--endpoint' must be an absolute URL.");
        }

        var credentialOptions = CredentialOptions.Create(commandLine);
        var tokenScopes = GetTokenScopes(commandLine, endpoint);
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

    private static string[] GetTokenScopes(CommandLine commandLine, Uri endpoint)
    {
        var audience = commandLine.GetOption("audience")
            ?? Environment.GetEnvironmentVariable("ACMEBOT_AUDIENCE")
            ?? endpoint.GetLeftPart(UriPartial.Authority);

        if (audience.EndsWith("/.default", StringComparison.Ordinal) || audience.EndsWith("/user_impersonation", StringComparison.Ordinal))
        {
            throw new CliException("Option '--audience' must be an application ID URI or endpoint origin, not a token scope.");
        }

        return [ToDefaultScope(audience)];
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

    private static string? GetOptionOrEnvironment(CommandLine commandLine, string optionName, string environmentName)
    {
        return commandLine.GetOption(optionName) ?? Environment.GetEnvironmentVariable(environmentName);
    }
}

internal enum OutputFormat
{
    Table,
    Json
}
