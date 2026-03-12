using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Acmebot.Acme.Models;

namespace Acmebot.Acme.Challenges;

public static class AcmeChallengeInstructions
{
    public static string CreateKeyAuthorization(AcmeAccountHandle account, AcmeChallengeResource challenge)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(challenge);

        var token = ValidateToken(challenge);
        return $"{token}.{account.Signer.GetThumbprint()}";
    }

    public static AcmeHttp01ChallengeInstruction CreateHttp01(AcmeAccountHandle account, AcmeChallengeResource challenge)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(challenge);

        EnsureChallengeType(challenge, AcmeChallengeTypes.Http01);
        var token = ValidateToken(challenge);

        return new AcmeHttp01ChallengeInstruction
        {
            Path = $"/.well-known/acme-challenge/{token}",
            Content = CreateKeyAuthorization(account, challenge)
        };
    }

    public static AcmeDns01ChallengeInstruction CreateDns01(
        AcmeAccountHandle account,
        AcmeAuthorizationResource authorization,
        AcmeChallengeResource challenge)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(challenge);

        EnsureChallengeType(challenge, AcmeChallengeTypes.Dns01);
        var keyAuthorization = CreateKeyAuthorization(account, challenge);
        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(keyAuthorization));

        return new AcmeDns01ChallengeInstruction
        {
            RecordName = $"_acme-challenge.{authorization.Identifier.Value.TrimEnd('.')}.",
            RecordValue = Base64Url.EncodeToString(digest)
        };
    }

    private static void EnsureChallengeType(AcmeChallengeResource challenge, AcmeChallengeType expectedType)
    {
        if (challenge.Type != expectedType)
        {
            throw new ArgumentException($"Expected an ACME {expectedType} challenge.", nameof(challenge));
        }
    }

    private static string ValidateToken(AcmeChallengeResource challenge)
    {
        if (string.IsNullOrWhiteSpace(challenge.Token) || !Base64Url.IsValid(challenge.Token.AsSpan()))
        {
            throw new InvalidOperationException("The ACME challenge token is missing or invalid.");
        }

        return challenge.Token;
    }
}

public sealed class AcmeHttp01ChallengeInstruction
{
    public required string Path { get; init; }

    public required string Content { get; init; }
}

public sealed class AcmeDns01ChallengeInstruction
{
    public required string RecordName { get; init; }

    public required string RecordValue { get; init; }
}

public static class AcmeChallengeTypes
{
    public static AcmeChallengeType Http01 { get; } = new("http-01");
    public static AcmeChallengeType Dns01 { get; } = new("dns-01");
}
