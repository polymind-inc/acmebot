using Azure.Identity;
using Azure.ResourceManager;

namespace Acmebot.App.Internal;

public class AzureEnvironment
{
    public required Uri AuthorityHost { get; init; }
    public required ArmEnvironment ResourceManager { get; init; }

    public static AzureEnvironment Get(string name) => s_environments[name];

    private static readonly Dictionary<string, AzureEnvironment> s_environments = new()
    {
        {
            "AzureCloud",
            new AzureEnvironment
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                ResourceManager = ArmEnvironment.AzurePublicCloud
            }
        },
        {
            "AzureChinaCloud",
            new AzureEnvironment
            {
                AuthorityHost = AzureAuthorityHosts.AzureChina,
                ResourceManager = ArmEnvironment.AzureChina
            }
        },
        {
            "AzureUSGovernment",
            new AzureEnvironment
            {
                AuthorityHost = AzureAuthorityHosts.AzureGovernment,
                ResourceManager = ArmEnvironment.AzureGovernment
            }
        }
    };
}
