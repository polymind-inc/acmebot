namespace Acmebot.App.Models;

public sealed record ConfigurationItem(
    ConfigurationSectionItem Acme,
    ConfigurationSectionItem General,
    IReadOnlyList<ConfigurationSectionItem> DnsProviders);

public sealed record ConfigurationSectionItem(string Name, IReadOnlyList<ConfigurationSetting> Settings);

public sealed record ConfigurationSetting(string Name, string? Value, bool? IsConfigured = null);
