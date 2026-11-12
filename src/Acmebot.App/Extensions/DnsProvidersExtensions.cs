using Acmebot.App.Infrastructure;
using Acmebot.App.Options;
using Acmebot.App.Providers;

using Azure.Core;
using Azure.Identity;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Extensions;

internal static class DnsProvidersExtensions
{
    public static IServiceCollection AddDnsProviders(this IServiceCollection services)
    {
        services.AddSingleton<IEnumerable<IDnsProvider>>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;
            var environment = provider.GetRequiredService<AzureEnvironment>();
            var tokenCredential = provider.GetRequiredService<TokenCredential>();

            var dnsProviders = new List<IDnsProvider>();

            dnsProviders.TryAdd(options.Akamai, o => new AkamaiEdgeDnsProvider(o));
            dnsProviders.TryAdd(options.AzureDns, o => new AzureDnsProvider(
                o,
                environment.ResourceManager,
                ResolveCredential(environment, tokenCredential, o.ManagedIdentityClientId)));
            dnsProviders.TryAdd(options.AzurePrivateDns, o => new AzurePrivateDnsProvider(
                o,
                environment.ResourceManager,
                ResolveCredential(environment, tokenCredential, o.ManagedIdentityClientId)));
            dnsProviders.TryAdd(options.Cloudflare, o => new CloudflareProvider(o));
            dnsProviders.TryAdd(options.CustomDns, o => new CustomDnsProvider(o));
            dnsProviders.TryAdd(options.DnsMadeEasy, o => new DnsMadeEasyProvider(o));
            dnsProviders.TryAdd(options.EasyDns, o => new EasyDnsProvider(o));
            dnsProviders.TryAdd(options.GandiLiveDns, o => new GandiLiveDnsProvider(o));
            dnsProviders.TryAdd(options.GoDaddy, o => new GoDaddyProvider(o));
            dnsProviders.TryAdd(options.GoogleDns, o => new GoogleDnsProvider(
                o,
                environment.ResourceManager.Audience,
                ResolveCredential(environment, tokenCredential, o.ManagedIdentityClientId)));
            dnsProviders.TryAdd(options.IonosDns, o => new IonosDnsProvider(o));
            dnsProviders.TryAdd(options.Ovh, o => new OvhProvider(o));
            dnsProviders.TryAdd(options.PowerDns, o => new PowerDnsProvider(o));
            dnsProviders.TryAdd(options.Regfish, o => new RegfishProvider(o));
            dnsProviders.TryAdd(options.Route53, o => new Route53Provider(
                o,
                environment.ResourceManager.Audience,
                ResolveCredential(environment, tokenCredential, o.ManagedIdentityClientId)));
            dnsProviders.TryAdd(options.TransIp, o => new TransIpProvider(options, o, tokenCredential));
            dnsProviders.TryAdd(options.UnitedDomains, o => new UnitedDomainsProvider(o));

            return dnsProviders;
        });

        return services;
    }

    public static void TryAdd<TOption>(this IList<IDnsProvider> dnsProviders, TOption? options, Func<TOption, IDnsProvider> factory)
    {
        if (options is not null)
        {
            dnsProviders.Add(factory(options));
        }
    }

    private static TokenCredential ResolveCredential(AzureEnvironment environment, TokenCredential tokenCredential, string? managedIdentityClientId)
    {
        if (string.IsNullOrWhiteSpace(managedIdentityClientId))
        {
            return tokenCredential;
        }

        var managedIdentityId = ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId);

        return new ManagedIdentityCredential(new ManagedIdentityCredentialOptions(managedIdentityId)
        {
            AuthorityHost = environment.AuthorityHost
        });
    }
}
