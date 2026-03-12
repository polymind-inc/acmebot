using Acmebot.Acme.Models;

namespace Acmebot.Acme;

public static class AcmeProfileValidation
{
    private static readonly IReadOnlyDictionary<string, string> s_emptyProfiles = new Dictionary<string, string>(0, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, string> GetAdvertisedProfiles(AcmeDirectoryResource directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        return directory.Metadata?.Profiles ?? s_emptyProfiles;
    }

    public static bool IsProfileAdvertised(AcmeDirectoryResource directory, string profile)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        return GetAdvertisedProfiles(directory).ContainsKey(profile);
    }

    public static string? GetProfileDescription(AcmeDirectoryResource directory, string profile)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        return GetAdvertisedProfiles(directory).GetValueOrDefault(profile);
    }

    public static void EnsureProfileIsAdvertised(AcmeDirectoryResource directory, string profile)
    {
        if (!IsProfileAdvertised(directory, profile))
        {
            throw new InvalidOperationException($"The ACME server does not advertise the '{profile}' profile.");
        }
    }
}
