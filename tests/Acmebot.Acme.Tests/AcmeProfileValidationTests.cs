using Acmebot.Acme.Models;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeProfileValidationTests
{
    [Fact]
    public void GetAdvertisedProfiles_ReturnsEmptyDictionaryWhenMetadataIsMissing()
    {
        var directory = new AcmeDirectoryResource
        {
            NewNonce = new Uri("https://example.com/acme/new-nonce"),
            NewAccount = new Uri("https://example.com/acme/new-account"),
            NewOrder = new Uri("https://example.com/acme/new-order")
        };

        var profiles = AcmeProfileValidation.GetAdvertisedProfiles(directory);

        Assert.Empty(profiles);
    }

    [Fact]
    public void GetProfileDescription_ReturnsConfiguredDescription()
    {
        var directory = CreateDirectoryWithProfiles();

        var description = AcmeProfileValidation.GetProfileDescription(directory, "tlsserver");

        Assert.Equal("TLS Server", description);
    }

    [Fact]
    public void EnsureProfileIsAdvertised_ThrowsWhenProfileIsMissing()
    {
        var directory = CreateDirectoryWithProfiles();

        var exception = Assert.Throws<InvalidOperationException>(() => AcmeProfileValidation.EnsureProfileIsAdvertised(directory, "missing"));

        Assert.Equal("The ACME server does not advertise the 'missing' profile.", exception.Message);
    }

    private static AcmeDirectoryResource CreateDirectoryWithProfiles()
    {
        return new AcmeDirectoryResource
        {
            NewNonce = new Uri("https://example.com/acme/new-nonce"),
            NewAccount = new Uri("https://example.com/acme/new-account"),
            NewOrder = new Uri("https://example.com/acme/new-order"),
            Metadata = new AcmeDirectoryMetadata
            {
                Profiles = new Dictionary<string, string>
                {
                    ["tlsserver"] = "TLS Server"
                }
            }
        };
    }
}
