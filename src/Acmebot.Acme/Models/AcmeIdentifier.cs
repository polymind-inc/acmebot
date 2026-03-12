using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acmebot.Acme.Models;

public sealed record AcmeIdentifier
{
    [JsonPropertyName("type")]
    public required AcmeIdentifierType Type { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

[JsonConverter(typeof(AcmeIdentifierTypeJsonConverter))]
public readonly record struct AcmeIdentifierType
{
    public AcmeIdentifierType(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(AcmeIdentifierType type) => type.Value;

    public static explicit operator AcmeIdentifierType(string value) => new(value);
}

public static class AcmeIdentifierTypes
{
    public static AcmeIdentifierType Dns { get; } = new("dns");
    public static AcmeIdentifierType Ip { get; } = new("ip");
}

internal sealed class AcmeIdentifierTypeJsonConverter : JsonConverter<AcmeIdentifierType>
{
    public override AcmeIdentifierType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("The ACME identifier type must be a non-empty string.");
        }

        return new AcmeIdentifierType(value);
    }

    public override void Write(Utf8JsonWriter writer, AcmeIdentifierType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
