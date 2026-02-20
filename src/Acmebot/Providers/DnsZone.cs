using System.Globalization;

namespace Acmebot.Providers;

public class DnsZone(IDnsProvider dnsProvider) : IEquatable<DnsZone>
{
    private static readonly IdnMapping s_idnMapping = new();

    public required string Id { get; init; }

    public required string Name
    {
        get;
        init => field = s_idnMapping.GetAscii(value);
    }

    public IReadOnlyList<string> NameServers { get; init; } = [];

    public IDnsProvider DnsProvider { get; } = dnsProvider;

    public bool Equals(DnsZone? other)
    {
        if (other is null)
        {
            return false;
        }

        return Id == other.Id;
    }

    public override bool Equals(object? obj) => Equals(obj as DnsZone);

    public override int GetHashCode() => Id.GetHashCode();
}
