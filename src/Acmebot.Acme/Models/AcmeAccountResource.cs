using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acmebot.Acme.Models;

public sealed record AcmeAccountResource
{
    [JsonPropertyName("status")]
    public required AcmeAccountStatus Status { get; init; }

    [JsonPropertyName("contact")]
    public IReadOnlyList<string> Contact { get; init; } = [];

    [JsonPropertyName("termsOfServiceAgreed")]
    public bool? TermsOfServiceAgreed { get; init; }

    [JsonPropertyName("externalAccountBinding")]
    public AcmeSignedMessage? ExternalAccountBinding { get; init; }

    [JsonPropertyName("orders")]
    public Uri? Orders { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed record AcmeNewAccountRequest
{
    [JsonPropertyName("contact")]
    public IReadOnlyList<string> Contact { get; init; } = [];

    [JsonPropertyName("termsOfServiceAgreed")]
    public bool? TermsOfServiceAgreed { get; init; }

    [JsonPropertyName("onlyReturnExisting")]
    public bool? OnlyReturnExisting { get; init; }
}

[JsonConverter(typeof(AcmeAccountStatusJsonConverter))]
public readonly record struct AcmeAccountStatus
{
    public AcmeAccountStatus(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(AcmeAccountStatus status) => status.Value;

    public static explicit operator AcmeAccountStatus(string value) => new(value);
}

public static class AcmeAccountStatuses
{
    public static AcmeAccountStatus Valid { get; } = new("valid");
    public static AcmeAccountStatus Deactivated { get; } = new("deactivated");
    public static AcmeAccountStatus Revoked { get; } = new("revoked");
}

internal sealed class AcmeAccountStatusJsonConverter : JsonConverter<AcmeAccountStatus>
{
    public override AcmeAccountStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("The ACME account status must be a non-empty string.");
        }

        return new AcmeAccountStatus(value);
    }

    public override void Write(Utf8JsonWriter writer, AcmeAccountStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed record AcmeUpdateAccountRequest
{
    [JsonPropertyName("contact")]
    public IReadOnlyList<string>? Contact { get; init; }
}

public sealed class AcmeExternalAccountBindingOptions
{
    public required string KeyIdentifier { get; init; }

    public required ReadOnlyMemory<byte> HmacKey { get; init; }

    public string Algorithm { get; init; } = "HS256";

    public static AcmeExternalAccountBindingOptions FromBase64Url(string keyIdentifier, string hmacKey, string algorithm = "HS256")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(hmacKey);

        return new AcmeExternalAccountBindingOptions
        {
            KeyIdentifier = keyIdentifier,
            HmacKey = System.Buffers.Text.Base64Url.DecodeFromChars(hmacKey),
            Algorithm = algorithm
        };
    }
}
