using System.Globalization;

namespace Acmebot.App.Providers;

public class DnsZone(IDnsProvider dnsProvider) : IEquatable<DnsZone>
{
    private static readonly IdnMapping s_idnMapping = new();
    private static readonly StringComparer s_stringComparer = StringComparer.Ordinal;

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

        return s_stringComparer.Equals(Id, other.Id) && s_stringComparer.Equals(DnsProvider.Name, other.DnsProvider.Name);
    }

    public override bool Equals(object? obj) => Equals(obj as DnsZone);

    public override int GetHashCode() => HashCode.Combine(s_stringComparer.GetHashCode(Id), s_stringComparer.GetHashCode(DnsProvider.Name));
}
