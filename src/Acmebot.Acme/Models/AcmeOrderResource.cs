using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acmebot.Acme.Models;

public sealed record AcmeOrderResource
{
    [JsonPropertyName("status")]
    public required AcmeOrderStatus Status { get; init; }

    [JsonPropertyName("expires")]
    public DateTimeOffset? Expires { get; init; }

    [JsonPropertyName("identifiers")]
    public IReadOnlyList<AcmeIdentifier> Identifiers { get; init; } = [];

    [JsonPropertyName("notBefore")]
    public DateTimeOffset? NotBefore { get; init; }

    [JsonPropertyName("notAfter")]
    public DateTimeOffset? NotAfter { get; init; }

    [JsonPropertyName("error")]
    public AcmeProblemDetails? Error { get; init; }

    [JsonPropertyName("authorizations")]
    public IReadOnlyList<Uri> Authorizations { get; init; } = [];

    [JsonPropertyName("finalize")]
    public Uri? Finalize { get; init; }

    [JsonPropertyName("certificate")]
    public Uri? Certificate { get; init; }

    [JsonPropertyName("replaces")]
    public string? Replaces { get; init; }

    [JsonPropertyName("profile")]
    public string? Profile { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed record AcmeOrderListResource
{
    [JsonPropertyName("orders")]
    public IReadOnlyList<Uri> Orders { get; init; } = [];
}

[JsonConverter(typeof(AcmeOrderStatusJsonConverter))]
public readonly record struct AcmeOrderStatus
{
    public AcmeOrderStatus(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(AcmeOrderStatus status) => status.Value;

    public static explicit operator AcmeOrderStatus(string value) => new(value);
}

public static class AcmeOrderStatuses
{
    public static AcmeOrderStatus Pending { get; } = new("pending");
    public static AcmeOrderStatus Ready { get; } = new("ready");
    public static AcmeOrderStatus Processing { get; } = new("processing");
    public static AcmeOrderStatus Valid { get; } = new("valid");
    public static AcmeOrderStatus Invalid { get; } = new("invalid");
}

internal sealed class AcmeOrderStatusJsonConverter : JsonConverter<AcmeOrderStatus>
{
    public override AcmeOrderStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("The ACME order status must be a non-empty string.");
        }

        return new AcmeOrderStatus(value);
    }

    public override void Write(Utf8JsonWriter writer, AcmeOrderStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed record AcmeNewOrderRequest
{
    [JsonPropertyName("identifiers")]
    public required IReadOnlyList<AcmeIdentifier> Identifiers { get; init; }

    [JsonPropertyName("notBefore")]
    public DateTimeOffset? NotBefore { get; init; }

    [JsonPropertyName("notAfter")]
    public DateTimeOffset? NotAfter { get; init; }

    [JsonPropertyName("replaces")]
    public string? Replaces { get; init; }

    [JsonPropertyName("profile")]
    public string? Profile { get; init; }
}

public sealed record AcmeFinalizeOrderRequest
{
    [JsonPropertyName("csr")]
    public required string Csr { get; init; }
}

public sealed record AcmeRevocationRequest
{
    [JsonPropertyName("certificate")]
    public required string Certificate { get; init; }

    [JsonPropertyName("reason")]
    public int? Reason { get; init; }
}
