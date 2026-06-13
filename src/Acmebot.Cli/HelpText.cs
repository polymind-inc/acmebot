namespace Acmebot.Cli;

internal static class HelpText
{
    public static async Task WriteAsync(TextWriter writer)
    {
        await writer.WriteLineAsync("Acmebot CLI");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("Usage:");
        await writer.WriteLineAsync("  acmebot --endpoint <url> certificate list [options]");
        await writer.WriteLineAsync("  acmebot --endpoint <url> certificate issue --dns-name <name> [options]");
        await writer.WriteLineAsync("  acmebot --endpoint <url> certificate renew <certificate-name> [options]");
        await writer.WriteLineAsync("  acmebot --endpoint <url> certificate revoke <certificate-name> [options]");
        await writer.WriteLineAsync("  acmebot --endpoint <url> dns-zone list [options]");
        await writer.WriteLineAsync("  acmebot --endpoint <url> operation wait <operation-location> [options]");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("Global options:");
        await writer.WriteLineAsync("  --endpoint <url>                       Acmebot application URL. Env: ACMEBOT_ENDPOINT");
        await writer.WriteLineAsync("  --audience <audience>                  Microsoft Entra application ID URI. Default: endpoint origin.");
        await writer.WriteLineAsync("  --tenant-id <id>                       Microsoft Entra tenant ID.");
        await writer.WriteLineAsync("  --client-id <id>                       Service principal client ID.");
        await writer.WriteLineAsync("  --client-secret <secret>               Service principal client secret.");
        await writer.WriteLineAsync("  --client-certificate-path <path>       Service principal certificate path.");
        await writer.WriteLineAsync("  --client-certificate-password <value>  PFX certificate password.");
        await writer.WriteLineAsync("  --managed-identity-client-id <id>      User-assigned managed identity client ID.");
        await writer.WriteLineAsync("  --format <table|json>                  Output format. Default: table.");
        await writer.WriteLineAsync("  --json                                 Shortcut for --format json.");
        await writer.WriteLineAsync("  --poll-interval <seconds>              Operation polling interval. Default: 5.");
        await writer.WriteLineAsync("  --timeout <seconds>                    Operation wait timeout. Default: 1800.");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("Certificate issue options:");
        await writer.WriteLineAsync("  --name <value>                         Key Vault certificate name.");
        await writer.WriteLineAsync("  --dns-name <value>                     DNS name. Repeatable.");
        await writer.WriteLineAsync("  --dns-provider <value>                 DNS provider display name.");
        await writer.WriteLineAsync("  --key-type <RSA|EC>                    Key type. Default: RSA.");
        await writer.WriteLineAsync("  --key-size <2048|3072|4096>            RSA key size. Default: 2048.");
        await writer.WriteLineAsync("  --key-curve <P-256|P-384|P-521|P-256K> EC key curve. Default: P-256.");
        await writer.WriteLineAsync("  --reuse-key                            Reuse the Key Vault certificate key.");
        await writer.WriteLineAsync("  --dns-alias <value>                    DNS-01 validation alias.");
        await writer.WriteLineAsync("  --tag <name=value>                     Certificate tag. Repeatable.");
        await writer.WriteLineAsync("  --no-wait                              Return after the operation is accepted.");
    }

    public static async Task WriteUsageHintAsync(TextWriter writer)
    {
        await writer.WriteLineAsync("Run 'acmebot --help' for usage.");
    }
}
