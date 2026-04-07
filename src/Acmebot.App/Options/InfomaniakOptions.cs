namespace Acmebot.App.Options;

/// <summary>
/// Configuration options for the Infomaniak DNS provider.
/// Requires an API token generated from the Infomaniak Manager with the "domain" scope.
/// </summary>
public class InfomaniakOptions
{
    /// <summary>OAuth2 Bearer token with DNS management permissions.</summary>
    public required string ApiToken { get; set; }
}
