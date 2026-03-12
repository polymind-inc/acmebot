using System.Net.Http.Headers;

using Acmebot.Acme.Models;

namespace Acmebot.Acme.Internal;

internal static class AcmeHeaderParser
{
    public static IReadOnlyList<AcmeLinkHeader> ParseLinkHeaders(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Link", out var values))
        {
            return [];
        }

        var links = new List<AcmeLinkHeader>();

        foreach (var value in values)
        {
            foreach (var linkValue in SplitHeaderValue(value, ','))
            {
                var parts = SplitHeaderValue(linkValue, ';');

                if (parts.Count == 0)
                {
                    continue;
                }

                var uriPart = parts[0];

                if (!uriPart.StartsWith('<') || !uriPart.EndsWith('>'))
                {
                    continue;
                }

                if (!Uri.TryCreate(uriPart[1..^1], UriKind.Absolute, out var uri))
                {
                    continue;
                }

                string? relation = null;
                string? mediaType = null;
                string? title = null;

                for (var i = 1; i < parts.Count; i++)
                {
                    var parameter = parts[i];
                    var separatorIndex = parameter.IndexOf('=');

                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var name = parameter[..separatorIndex].Trim();
                    var rawValue = parameter[(separatorIndex + 1)..].Trim().Trim('"');

                    switch (name)
                    {
                        case "rel":
                            relation = rawValue;
                            break;
                        case "type":
                            mediaType = rawValue;
                            break;
                        case "title":
                            title = rawValue;
                            break;
                    }
                }

                links.Add(new AcmeLinkHeader
                {
                    Uri = uri,
                    Relation = relation,
                    MediaType = mediaType,
                    Title = title
                });
            }
        }

        return links;
    }

    private static List<string> SplitHeaderValue(string value, char separator)
    {
        var parts = new List<string>();
        var startIndex = 0;
        var inQuotes = false;
        var inAngleBrackets = false;

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (current == '"' && !IsEscaped(value, i))
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
            {
                continue;
            }

            if (current == '<')
            {
                inAngleBrackets = true;
                continue;
            }

            if (current == '>')
            {
                inAngleBrackets = false;
                continue;
            }

            if (current == separator && !inAngleBrackets)
            {
                AddPart(value, startIndex, i - startIndex, parts);
                startIndex = i + 1;
            }
        }

        AddPart(value, startIndex, value.Length - startIndex, parts);
        return parts;
    }

    private static void AddPart(string value, int startIndex, int length, List<string> parts)
    {
        var segment = value.AsSpan(startIndex, length).Trim();

        if (!segment.IsEmpty)
        {
            parts.Add(segment.ToString());
        }
    }

    private static bool IsEscaped(string value, int index)
    {
        var backslashCount = 0;

        for (var i = index - 1; i >= 0 && value[i] == '\\'; i--)
        {
            backslashCount++;
        }

        return (backslashCount & 1) == 1;
    }
}
