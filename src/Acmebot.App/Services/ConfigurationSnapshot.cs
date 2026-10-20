using Acmebot.App.Models;
using Acmebot.App.Options;

namespace Acmebot.App.Services;

internal static class ConfigurationSnapshot
{
    // Explicitly project displayable fields. Never serialize options or discover fields by reflection.
    public static ConfigurationItem Create(AcmebotOptions options)
    {
        var providers = new List<ConfigurationSectionItem>();

        if (options.Akamai is { } akamai)
        {
            Add("Akamai Edge DNS", Text("Endpoint", DisplayUrl($"https://{akamai.Host}")), Secret("Client token", akamai.ClientToken), Secret("Client secret", akamai.ClientSecret), Secret("Access token", akamai.AccessToken));
        }

        if (options.AzureDns is { } azure)
        {
            Add("Azure DNS", Text("Subscription ID", azure.SubscriptionId), Text("Managed identity client ID", azure.ManagedIdentityClientId));
        }

        if (options.AzurePrivateDns is { } azurePrivate)
        {
            Add("Azure Private DNS", Text("Subscription ID", azurePrivate.SubscriptionId), Text("Managed identity client ID", azurePrivate.ManagedIdentityClientId));
        }

        if (options.Cloudflare is { } cloudflare)
        {
            Add("Cloudflare", Secret("API token", cloudflare.ApiToken));
        }

        if (options.CustomDns is { } custom)
        {
            Add("Custom DNS", Text("Endpoint", DisplayUrl(custom.Endpoint)), Secret("API key", custom.ApiKey), Text("API key header name", custom.ApiKeyHeaderName), Text("Propagation delay (seconds)", custom.PropagationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (options.DnsMadeEasy is { } dnsMadeEasy)
        {
            Add("DNS Made Easy", Secret("API key", dnsMadeEasy.ApiKey), Secret("Secret key", dnsMadeEasy.SecretKey));
        }

        if (options.GandiLiveDns is { } gandi)
        {
            Add("Gandi LiveDNS", Secret("API key", gandi.ApiKey));
        }

        if (options.GoDaddy is { } goDaddy)
        {
            Add("GoDaddy", Secret("API key", goDaddy.ApiKey), Secret("API secret", goDaddy.ApiSecret));
        }

        if (options.GoogleDns is { } google)
        {
            Add("Google Cloud DNS", Text("Project ID", google.ProjectId), Secret("Service account key", google.KeyFile64), Text("Workload identity provider", google.PoolProvider), Text("Service account", google.ServiceAccount), Text("Managed identity client ID", google.ManagedIdentityClientId));
        }

        if (options.IonosDns is { } ionos)
        {
            Add("IONOS DNS", Secret("API key", ionos.ApiKey));
        }

        if (options.Ovh is { } ovh)
        {
            Add("OVH", Text("Endpoint", DisplayUrl(ovh.Endpoint)), Secret("Application key", ovh.ApplicationKey), Secret("Application secret", ovh.ApplicationSecret), Secret("Consumer key", ovh.ConsumerKey));
        }

        if (options.PowerDns is { } power)
        {
            Add("PowerDNS", Text("Endpoint", DisplayUrl(power.Endpoint?.ToString())), Secret("API key", power.ApiKey), Text("Server ID", power.ServerId));
        }

        if (options.Regfish is { } regfish)
        {
            Add("Regfish", Secret("API key", regfish.ApiKey));
        }

        if (options.Route53 is { } route53)
        {
            Add("Amazon Route 53", Text("Role ARN", route53.RoleArn), Text("Managed identity client ID", route53.ManagedIdentityClientId), Secret("Access key", route53.AccessKey), Secret("Secret key", route53.SecretKey));
        }

        if (options.TransIp is { } transIp)
        {
            Add("TransIP DNS", Text("Customer name", transIp.CustomerName), Secret("Private key reference", transIp.PrivateKeyName));
        }

        if (options.UnitedDomains is { } united)
        {
            Add("UnitedDomains", Secret("API key", united.ApiKey));
        }

        return new ConfigurationItem(
            new ConfigurationSectionItem("ACME CA", [
                Text("Directory URL", DisplayUrl(options.Endpoint.ToString())),
                Text("Contact email", options.Contacts),
                Text("Preferred chain", options.PreferredChain),
                Text("Preferred profile", options.PreferredProfile),
                Secret("EAB key ID", options.ExternalAccountBinding?.KeyId),
                Secret("EAB HMAC key", options.ExternalAccountBinding?.HmacKey),
                Text("EAB algorithm", options.ExternalAccountBinding?.Algorithm)
            ]),
            new ConfigurationSectionItem("General", [
                Text("Azure environment", options.Environment),
                Text("Key Vault URL", DisplayUrl(options.VaultBaseUrl)),
                Text("Managed identity client ID", options.ManagedIdentityClientId),
                Text("Renewal threshold (% of lifetime remaining)", options.RenewBeforeExpiry.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Text("Require app roles", options.RequireAppRoles ? "Yes" : "No"),
                Text("Use system name server", options.UseSystemNameServer ? "Yes" : "No")
            ]),
            providers);

        void Add(string name, params ConfigurationSetting[] settings) => providers.Add(new ConfigurationSectionItem(name, settings));
    }

    private static ConfigurationSetting Text(string name, string? value) => new(name, string.IsNullOrWhiteSpace(value) ? null : value);

    private static ConfigurationSetting Secret(string name, string? value) => new(name, null, !string.IsNullOrWhiteSpace(value));

    private static string? DisplayUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            return null;
        }

        // User info, queries and fragments can contain credentials. Never return them to the browser.
        return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
    }
}
