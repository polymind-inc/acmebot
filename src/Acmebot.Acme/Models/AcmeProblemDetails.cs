using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acmebot.Acme.Models;

public sealed record AcmeProblemDetails
{
    [JsonPropertyName("type")]
    public AcmeProblemType? Type { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    [JsonPropertyName("identifier")]
    public AcmeIdentifier? Identifier { get; init; }

    [JsonPropertyName("subproblems")]
    public IReadOnlyList<AcmeProblemDetails> Subproblems { get; init; } = [];

    [JsonPropertyName("algorithms")]
    public IReadOnlyList<string> Algorithms { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

[JsonConverter(typeof(AcmeProblemTypeJsonConverter))]
public readonly record struct AcmeProblemType
{
    public AcmeProblemType(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(AcmeProblemType type) => type.Value;

    public static explicit operator AcmeProblemType(string value) => new(value);
}

public static class AcmeProblemTypes
{
    public static AcmeProblemType AccountDoesNotExist { get; } = new("urn:ietf:params:acme:error:accountDoesNotExist");
    public static AcmeProblemType AlreadyReplaced { get; } = new("urn:ietf:params:acme:error:alreadyReplaced");
    public static AcmeProblemType AlreadyRevoked { get; } = new("urn:ietf:params:acme:error:alreadyRevoked");
    public static AcmeProblemType BadCertificateSigningRequest { get; } = new("urn:ietf:params:acme:error:badCSR");
    public static AcmeProblemType BadNonce { get; } = new("urn:ietf:params:acme:error:badNonce");
    public static AcmeProblemType BadPublicKey { get; } = new("urn:ietf:params:acme:error:badPublicKey");
    public static AcmeProblemType BadRevocationReason { get; } = new("urn:ietf:params:acme:error:badRevocationReason");
    public static AcmeProblemType BadSignatureAlgorithm { get; } = new("urn:ietf:params:acme:error:badSignatureAlgorithm");
    public static AcmeProblemType Caa { get; } = new("urn:ietf:params:acme:error:caa");
    public static AcmeProblemType Compound { get; } = new("urn:ietf:params:acme:error:compound");
    public static AcmeProblemType Connection { get; } = new("urn:ietf:params:acme:error:connection");
    public static AcmeProblemType Dns { get; } = new("urn:ietf:params:acme:error:dns");
    public static AcmeProblemType ExternalAccountRequired { get; } = new("urn:ietf:params:acme:error:externalAccountRequired");
    public static AcmeProblemType IncorrectResponse { get; } = new("urn:ietf:params:acme:error:incorrectResponse");
    public static AcmeProblemType InvalidContact { get; } = new("urn:ietf:params:acme:error:invalidContact");
    public static AcmeProblemType InvalidProfile { get; } = new("urn:ietf:params:acme:error:invalidProfile");
    public static AcmeProblemType Malformed { get; } = new("urn:ietf:params:acme:error:malformed");
    public static AcmeProblemType OrderNotReady { get; } = new("urn:ietf:params:acme:error:orderNotReady");
    public static AcmeProblemType RateLimited { get; } = new("urn:ietf:params:acme:error:rateLimited");
    public static AcmeProblemType RejectedIdentifier { get; } = new("urn:ietf:params:acme:error:rejectedIdentifier");
    public static AcmeProblemType ServerInternal { get; } = new("urn:ietf:params:acme:error:serverInternal");
    public static AcmeProblemType Tls { get; } = new("urn:ietf:params:acme:error:tls");
    public static AcmeProblemType Unauthorized { get; } = new("urn:ietf:params:acme:error:unauthorized");
    public static AcmeProblemType UnsupportedContact { get; } = new("urn:ietf:params:acme:error:unsupportedContact");
    public static AcmeProblemType UnsupportedIdentifier { get; } = new("urn:ietf:params:acme:error:unsupportedIdentifier");
    public static AcmeProblemType UserActionRequired { get; } = new("urn:ietf:params:acme:error:userActionRequired");
}

internal sealed class AcmeProblemTypeJsonConverter : JsonConverter<AcmeProblemType>
{
    public override AcmeProblemType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("The ACME problem type must be a non-empty string.");
        }

        return new AcmeProblemType(value);
    }

    public override void Write(Utf8JsonWriter writer, AcmeProblemType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
