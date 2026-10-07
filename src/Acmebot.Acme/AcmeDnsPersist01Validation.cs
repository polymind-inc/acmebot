using System.Globalization;

using Acmebot.Acme.Challenges;
using Acmebot.Acme.Models;

namespace Acmebot.Acme;

/// <summary>Validates DNS-PERSIST-01 discovery and challenges against draft-ietf-acme-dns-persist-02.</summary>
public static class AcmeDnsPersist01Validation
{
    private static readonly IdnMapping s_idnMapping = new() { UseStd3AsciiRules = true };

    /// <summary>Checks whether the directory provides usable pre-provisioning inputs, independently of CAA identities.</summary>
    public static bool IsPreProvisioningAvailable(AcmeDirectoryResource directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        return GetPreProvisioningError(directory) is null;
    }

    /// <summary>Throws an actionable error when pre-provisioning metadata is missing or malformed.</summary>
    public static void EnsurePreProvisioningIsAvailable(AcmeDirectoryResource directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (GetPreProvisioningError(directory) is { } error)
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <summary>
    /// Validates an offered challenge. Missing or invalid directory issuer names do not
    /// prevent interactive validation; valid directory and CAA names constrain the challenge.
    /// Call this when selecting DNS-PERSIST-01, rather than rejecting unrelated challenges.
    /// </summary>
    public static void EnsureChallengeIsUsable(AcmeDirectoryResource directory, AcmeChallengeResource challenge)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(challenge);

        if (challenge.Type != AcmeChallengeTypes.DnsPersist01)
        {
            throw new ArgumentException("Expected an ACME dns-persist-01 challenge.", nameof(challenge));
        }

        if (challenge.Url is null || !challenge.Url.IsAbsoluteUri || challenge.Status is null || string.IsNullOrEmpty(challenge.Status.Value.Value))
        {
            throw new InvalidOperationException("The DNS-PERSIST-01 challenge must contain an absolute url and a status.");
        }

        if (!AreIssuerDomainNamesValid(challenge.IssuerDomainNames))
        {
            throw new InvalidOperationException("The DNS-PERSIST-01 challenge issuerDomainNames must contain 1 to 10 lowercase A-label domain names without trailing dots, each at most 253 octets.");
        }

        if (!IsAccountHashPrefixValid(directory.Metadata?.AccountHashPrefix))
        {
            throw new InvalidOperationException("The ACME directory accountHashPrefix is missing or cannot form a valid absolute hashed account URI.");
        }

        var directoryNames = directory.Metadata?.IssuerDomainNames;
        if (AreIssuerDomainNamesValid(directoryNames) && directoryNames!.Except(challenge.IssuerDomainNames, StringComparer.Ordinal).Any())
        {
            throw new InvalidOperationException("The DNS-PERSIST-01 challenge issuerDomainNames does not include every issuer advertised by the directory.");
        }

        var caaIdentities = NormalizeCaaIdentities(directory.Metadata?.CaaIdentities);
        if (caaIdentities is not null && challenge.IssuerDomainNames.Except(caaIdentities, StringComparer.Ordinal).Any())
        {
            throw new InvalidOperationException("The DNS-PERSIST-01 challenge issuerDomainNames contains an issuer absent from the directory caaIdentities.");
        }
    }

    private static string? GetPreProvisioningError(AcmeDirectoryResource directory)
    {
        if (!AreIssuerDomainNamesValid(directory.Metadata?.IssuerDomainNames))
        {
            return "DNS-PERSIST-01 pre-provisioning is unavailable: the directory issuerDomainNames must contain 1 to 10 lowercase A-label domain names without trailing dots, each at most 253 octets.";
        }

        return IsAccountHashPrefixValid(directory.Metadata?.AccountHashPrefix)
            ? null
            : "DNS-PERSIST-01 pre-provisioning is unavailable: the directory accountHashPrefix is missing or cannot form a valid absolute hashed account URI.";
    }

    private static bool AreIssuerDomainNamesValid(IReadOnlyList<string>? names) =>
        names is { Count: >= 1 and <= 10 } && names.All(IsNormalizedDomainName);

    private static bool IsNormalizedDomainName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 253)
        {
            return false;
        }

        foreach (var label in name.Split('.'))
        {
            if (label.Length is < 1 or > 63 || label[0] == '-' || label[^1] == '-' ||
                label.Any(c => c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-'))
            {
                return false;
            }
        }

        try
        {
            return s_idnMapping.GetAscii(s_idnMapping.GetUnicode(name)).ToLowerInvariant() == name;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string>? NormalizeCaaIdentities(IReadOnlyList<string>? identities)
    {
        if (identities is not { Count: > 0 })
        {
            return null;
        }

        var normalized = new List<string>();
        foreach (var identity in identities)
        {
            if (string.IsNullOrEmpty(identity))
            {
                return null;
            }

            try
            {
                var name = s_idnMapping.GetAscii(identity.EndsWith('.') ? identity[..^1] : identity).ToLowerInvariant();
                if (!IsNormalizedDomainName(name))
                {
                    return null;
                }
                normalized.Add(name);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return normalized;
    }

    private static bool IsAccountHashPrefixValid(string? prefix) =>
        !string.IsNullOrEmpty(prefix) && !prefix.Any(c => c > 127 || char.IsWhiteSpace(c) || char.IsControl(c)) &&
        Uri.TryCreate(prefix + "sha-256/" + new string('A', 43), UriKind.Absolute, out var uri) && uri.IsWellFormedOriginalString();
}
