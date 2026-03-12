using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Acmebot.Acme.Challenges;
using Acmebot.Acme.Models;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeChallengeInstructionsTests
{
    [Fact]
    public void CreateHttp01_ReturnsExpectedHttpPathAndContent()
    {
        using var signer = AcmeSigner.CreateP256();
        var account = AcmeTestSupport.CreateAccountHandle(signer);
        var challenge = new AcmeChallengeResource
        {
            Type = AcmeChallengeTypes.Http01,
            Url = new Uri("https://example.com/acme/challenge/1"),
            Token = "dG9rZW4"
        };

        var instruction = AcmeChallengeInstructions.CreateHttp01(account, challenge);

        Assert.Equal("/.well-known/acme-challenge/dG9rZW4", instruction.Path);
        Assert.Equal($"dG9rZW4.{signer.GetThumbprint()}", instruction.Content);
    }

    [Fact]
    public void CreateDns01_ReturnsExpectedRecordNameAndValue()
    {
        using var signer = AcmeSigner.CreateP256();
        var account = AcmeTestSupport.CreateAccountHandle(signer);
        var authorization = new AcmeAuthorizationResource
        {
            Identifier = new AcmeIdentifier
            {
                Type = AcmeIdentifierTypes.Dns,
                Value = "example.com."
            },
            Status = AcmeAuthorizationStatuses.Pending
        };
        var challenge = new AcmeChallengeResource
        {
            Type = AcmeChallengeTypes.Dns01,
            Url = new Uri("https://example.com/acme/challenge/2"),
            Token = "dG9rZW4"
        };

        var instruction = AcmeChallengeInstructions.CreateDns01(account, authorization, challenge);
        var keyAuthorization = $"dG9rZW4.{signer.GetThumbprint()}";
        var expectedValue = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(keyAuthorization)));

        Assert.Equal("_acme-challenge.example.com.", instruction.RecordName);
        Assert.Equal(expectedValue, instruction.RecordValue);
    }

    [Fact]
    public void CreateHttp01_ThrowsWhenTokenIsInvalid()
    {
        using var signer = AcmeSigner.CreateP256();
        var account = AcmeTestSupport.CreateAccountHandle(signer);
        var challenge = new AcmeChallengeResource
        {
            Type = AcmeChallengeTypes.Http01,
            Url = new Uri("https://example.com/acme/challenge/1"),
            Token = "not+valid"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => AcmeChallengeInstructions.CreateHttp01(account, challenge));

        Assert.Equal("The ACME challenge token is missing or invalid.", exception.Message);
    }

    [Fact]
    public void CreateDns01_ThrowsWhenChallengeTypeIsInvalid()
    {
        using var signer = AcmeSigner.CreateP256();
        var account = AcmeTestSupport.CreateAccountHandle(signer);
        var authorization = new AcmeAuthorizationResource
        {
            Identifier = new AcmeIdentifier
            {
                Type = AcmeIdentifierTypes.Dns,
                Value = "example.com"
            },
            Status = AcmeAuthorizationStatuses.Pending
        };
        var challenge = new AcmeChallengeResource
        {
            Type = AcmeChallengeTypes.Http01,
            Url = new Uri("https://example.com/acme/challenge/1"),
            Token = "dG9rZW4"
        };

        var exception = Assert.Throws<ArgumentException>(() => AcmeChallengeInstructions.CreateDns01(account, authorization, challenge));

        Assert.Equal("Expected an ACME dns-01 challenge. (Parameter 'challenge')", exception.Message);
    }
}
