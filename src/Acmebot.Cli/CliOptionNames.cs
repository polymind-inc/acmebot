namespace Acmebot.Cli;

internal static class CliOptionNames
{
    public static readonly HashSet<string> OptionsWithValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "endpoint",
        "audience",
        "config",
        "tenant-id",
        "client-id",
        "client-secret",
        "client-certificate-path",
        "client-certificate-password",
        "managed-identity-client-id",
        "format",
        "poll-interval",
        "timeout",
        "name",
        "dns-name",
        "dns-provider",
        "key-type",
        "key-size",
        "key-curve",
        "dns-alias",
        "tag"
    };

    public static readonly HashSet<string> KnownOptions = new(OptionsWithValues, StringComparer.OrdinalIgnoreCase)
    {
        "json",
        "reuse-key",
        "no-wait",
        "help"
    };
}
