using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acmebot.Acme.Models;

public sealed record AcmeAuthorizationResource
{
    [JsonPropertyName("identifier")]
    public required AcmeIdentifier Identifier { get; init; }

    [JsonPropertyName("status")]
    public required AcmeAuthorizationStatus Status { get; init; }

    [JsonPropertyName("expires")]
    public DateTimeOffset? Expires { get; init; }

    [JsonPropertyName("challenges")]
    public IReadOnlyList<AcmeChallengeResource> Challenges { get; init; } = [];

    [JsonPropertyName("wildcard")]
    public bool? Wildcard { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed record AcmeChallengeResource
{
    [JsonPropertyName("type")]
    public required AcmeChallengeType Type { get; init; }

    [JsonPropertyName("url")]
    public required Uri Url { get; init; }

    [JsonPropertyName("status")]
    public AcmeChallengeStatus? Status { get; init; }

    [JsonPropertyName("validated")]
    public DateTimeOffset? Validated { get; init; }

    [JsonPropertyName("error")]
    public AcmeProblemDetails? Error { get; init; }

    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed record AcmeNewAuthorizationRequest
{
    [JsonPropertyName("identifier")]
    public required AcmeIdentifier Identifier { get; init; }
}

[JsonConverter(typeof(AcmeAuthorizationStatusJsonConverter))]
public readonly record struct AcmeAuthorizationStatus
{
    public AcmeAuthorizationStatus(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(AcmeAuthorizationStatus status) => status.Value;

    public static explicit operator AcmeAuthorizationStatus(string value) => new(value);
}

public static class AcmeAuthorizationStatuses
{
    public static AcmeAuthorizationStatus Pending { get; } = new("pending");
    public static AcmeAuthorizationStatus Valid { get; } = new("valid");
    public static AcmeAuthorizationStatus Invalid { get; } = new("invalid");
    public static AcmeAuthorizationStatus Deactivated { get; } = new("deactivated");
    public static AcmeAuthorizationStatus Expired { get; } = new("expired");
    public static AcmeAuthorizationStatus Revoked { get; } = new("revoked");
}

internal sealed class AcmeAuthorizationStatusJsonConverter : JsonConverter<AcmeAuthorizationStatus>
{
    public override AcmeAuthorizationStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("The ACME authorization status must be a non-empty string.");
        }

        return new AcmeAuthorizationStatus(value);
    }

    public override void Write(Utf8JsonWriter writer, AcmeAuthorizationStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

[JsonConverter(typeof(AcmeChallengeTypeJsonConverter))]
public readonly record struct AcmeChallengeType
{
    public AcmeChallengeType(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(AcmeChallengeType type) => type.Value;

    public static explicit operator AcmeChallengeType(string value) => new(value);
}

internal sealed class AcmeChallengeTypeJsonConverter : JsonConverter<AcmeChallengeType>
{
    public override AcmeChallengeType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("The ACME challenge type must be a non-empty string.");
        }

        return new AcmeChallengeType(value);
    }

    public override void Write(Utf8JsonWriter writer, AcmeChallengeType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

[JsonConverter(typeof(AcmeChallengeStatusJsonConverter))]
public readonly record struct AcmeChallengeStatus
{
    public AcmeChallengeStatus(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(AcmeChallengeStatus status) => status.Value;

    public static explicit operator AcmeChallengeStatus(string value) => new(value);
}

public static class AcmeChallengeStatuses
{
    public static AcmeChallengeStatus Pending { get; } = new("pending");
    public static AcmeChallengeStatus Processing { get; } = new("processing");
    public static AcmeChallengeStatus Valid { get; } = new("valid");
    public static AcmeChallengeStatus Invalid { get; } = new("invalid");
}

internal sealed class AcmeChallengeStatusJsonConverter : JsonConverter<AcmeChallengeStatus>
{
    public override AcmeChallengeStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("The ACME challenge status must be a non-empty string.");
        }

        return new AcmeChallengeStatus(value);
    }

    public override void Write(Utf8JsonWriter writer, AcmeChallengeStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
