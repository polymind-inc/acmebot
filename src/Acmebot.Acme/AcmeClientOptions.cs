namespace Acmebot.Acme;

public sealed class AcmeClientOptions
{
    public string UserAgent { get; set; } = "Acmebot.Acme/1.0";

    public string? AcceptLanguage { get; set; }

    public int BadNonceRetryCount { get; set; } = 1;
}
