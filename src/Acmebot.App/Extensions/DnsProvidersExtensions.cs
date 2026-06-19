using Acmebot.App.Providers;

namespace Acmebot.App.Extensions;

internal static class DnsProvidersExtensions
{
    public static void TryAdd<TOption>(this IList<IDnsProvider> dnsProviders, TOption? options, Func<TOption, IDnsProvider> factory)
    {
        if (options is not null)
        {
            dnsProviders.Add(factory(options));
        }
    }
}
