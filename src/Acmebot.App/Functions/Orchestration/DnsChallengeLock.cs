using Acmebot.App.Models;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace Acmebot.App.Functions.Orchestration;

// Durable entity locks serialize DNS-01 validation through cleanup, not just
// individual Azure DNS writes. No entity operations or persistent state are needed.
public sealed class DnsChallengeLock
{
    [Function(nameof(DnsChallengeLock))]
    public static Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
        => dispatcher.DispatchAsync<DnsChallengeLock>();

    internal static EntityInstanceId[] ForCertificate(CertificatePolicyItem policy)
        => policy.AliasedDnsNames
                 .Select(name => name.StartsWith("*.", StringComparison.Ordinal) ? name[2..] : name)
                 .Select(name => $"_acme-challenge.{name.TrimEnd('.').ToLowerInvariant()}")
                 .Distinct(StringComparer.Ordinal)
                 .OrderBy(name => name, StringComparer.Ordinal)
                 .Select(name => new EntityInstanceId(nameof(DnsChallengeLock), name))
                 .ToArray();
}
