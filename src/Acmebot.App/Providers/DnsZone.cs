using System.Globalization;

namespace Acmebot.App.Providers;

public class DnsZone
{
    private static readonly IdnMapping s_idnMapping = new();

    public required string Id { get; init; }

    public required string Name
    {
        get;
        init => field = s_idnMapping.GetAscii(value);
    }

    public IReadOnlyList<string> NameServers { get; init; } = [];
}
