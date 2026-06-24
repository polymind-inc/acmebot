using System.Globalization;
using System.Text.RegularExpressions;

namespace Acmebot.Cli;

internal static partial class CertificatePolicyFactory
{
    private static readonly IdnMapping s_idnMapping = new();
    private static readonly HashSet<int> s_rsaKeySizes = [2048, 3072, 4096];
    private static readonly Dictionary<string, string> s_ecKeyCurves = new(StringComparer.OrdinalIgnoreCase)
    {
        ["P-256"] = "P-256",
        ["P-384"] = "P-384",
        ["P-521"] = "P-521",
        ["P-256K"] = "P-256K"
    };

    public static CertificatePolicyItem Create(CommandLine commandLine)
    {
        var dnsNames = commandLine.GetOptions("dns-name")
            .Select(dnsName => NormalizeDnsNameOption(dnsName, "--dns-name", allowWildcard: true))
            .Where(dnsName => dnsName is not null)
            .Select(dnsName => dnsName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (dnsNames.Length == 0)
        {
            throw new CliException("At least one '--dns-name' value is required.");
        }

        var dnsProviderName = NormalizeOptionalValue(commandLine.GetOption("dns-provider"));

        if (dnsProviderName is null)
        {
            throw new CliException("Option '--dns-provider' is required.");
        }

        var customCertificateName = NormalizeOptionalValue(commandLine.GetOption("name"));
        var certificateName = customCertificateName ?? CreateCertificateName(dnsNames[0]);

        if (!CertificateNameRegex().IsMatch(certificateName))
        {
            throw new CliException(customCertificateName is null
                ? "Generated certificate name must be 1 to 127 characters and contain only letters, numbers, and hyphens. Specify '--name' to use a shorter name."
                : "Option '--name' must be 1 to 127 characters and contain only letters, numbers, and hyphens.");
        }

        var keyType = commandLine.GetOption("key-type")?.ToUpperInvariant() ?? "RSA";
        var keyCurveName = NormalizeOptionalValue(commandLine.GetOption("key-curve"));
        int? keySize = null;

        if (string.Equals(keyType, "RSA", StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(keyCurveName))
            {
                throw new CliException("Option '--key-curve' can only be used when '--key-type EC' is specified.");
            }

            keySize = ParseOptionalInt(commandLine.GetOption("key-size")) ?? 2048;

            if (!s_rsaKeySizes.Contains(keySize.Value))
            {
                throw new CliException("Option '--key-size' must be 2048, 3072, or 4096.");
            }
        }
        else if (string.Equals(keyType, "EC", StringComparison.Ordinal))
        {
            if (commandLine.HasOption("key-size"))
            {
                throw new CliException("Option '--key-size' can only be used when '--key-type RSA' is specified.");
            }

            keyCurveName = NormalizeEcKeyCurve(keyCurveName) ?? "P-256";

            if (!s_ecKeyCurves.ContainsKey(keyCurveName))
            {
                throw new CliException("Option '--key-curve' must be P-256, P-384, P-521, or P-256K.");
            }
        }
        else
        {
            throw new CliException("Option '--key-type' must be 'RSA' or 'EC'.");
        }

        return new CertificatePolicyItem
        {
            CertificateName = certificateName,
            DnsNames = dnsNames,
            DnsProviderName = dnsProviderName,
            KeyType = keyType,
            KeySize = keySize,
            KeyCurveName = keyCurveName,
            ReuseKey = commandLine.HasOption("reuse-key") ? commandLine.GetFlag("reuse-key") : null,
            DnsAlias = NormalizeDnsNameOption(commandLine.GetOption("dns-alias"), "--dns-alias", allowWildcard: false),
            Profile = NormalizeOptionalValue(commandLine.GetOption("profile")),
            Tags = ParseTags(commandLine.GetOptions("tag"))
        };
    }

    private static Dictionary<string, string>? ParseTags(IReadOnlyList<string> tagValues)
    {
        if (tagValues.Count == 0)
        {
            return null;
        }

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tagValue in tagValues)
        {
            var separatorIndex = tagValue.IndexOf('=');

            if (separatorIndex <= 0)
            {
                throw new CliException("Option '--tag' must use the form 'name=value'.");
            }

            var name = tagValue[..separatorIndex].Trim();
            var value = tagValue[(separatorIndex + 1)..];

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new CliException("Tag name cannot be empty.");
            }

            if (!tags.TryAdd(name, value))
            {
                throw new CliException($"Duplicate tag '{name}'.");
            }

            if (string.Equals(name, "Acmebot", StringComparison.OrdinalIgnoreCase))
            {
                throw new CliException("The 'Acmebot' tag is reserved for internal metadata.");
            }
        }

        return tags;
    }

    private static int? ParseOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
        {
            throw new CliException("Option '--key-size' must be a number.");
        }

        return result;
    }

    private static string? NormalizeOptionalValue(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeDnsNameOption(string? value, string optionName, bool allowWildcard)
    {
        var normalizedValue = NormalizeOptionalValue(value);

        if (normalizedValue is null)
        {
            return null;
        }

        var dnsName = normalizedValue.TrimEnd('.');

        if (dnsName.Length == 0)
        {
            throw new CliException($"Option '{optionName}' cannot be empty.");
        }

        var labels = dnsName.Split('.');

        for (var index = 0; index < labels.Length; index++)
        {
            if (labels[index] == "*")
            {
                if (!allowWildcard)
                {
                    throw new CliException($"Option '{optionName}' cannot be a wildcard.");
                }

                if (index != 0)
                {
                    throw new CliException("A wildcard can only be the leftmost DNS label.");
                }

                continue;
            }

            try
            {
                labels[index] = s_idnMapping.GetAscii(labels[index]).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                throw new CliException($"Option '{optionName}' contains characters that cannot be converted to a DNS name.");
            }
        }

        return string.Join('.', labels);
    }

    private static string CreateCertificateName(string dnsName) => dnsName.Replace("*", "wildcard").Replace(".", "-");

    private static string? NormalizeEcKeyCurve(string? value)
    {
        return value is not null && s_ecKeyCurves.TryGetValue(value, out var canonicalName)
            ? canonicalName
            : value;
    }

    [GeneratedRegex("^[0-9A-Za-z-]{1,127}$")]
    private static partial Regex CertificateNameRegex();
}
