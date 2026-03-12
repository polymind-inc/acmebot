using System.Text.Json.Serialization;

using Acmebot.Acme.Models;

namespace Acmebot.Acme.Internal;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(AcmeAccountResource))]
[JsonSerializable(typeof(AcmeAccountStatus))]
[JsonSerializable(typeof(AcmeAccountStatusUpdateRequest))]
[JsonSerializable(typeof(AcmeAuthorizationResource))]
[JsonSerializable(typeof(AcmeAuthorizationStatus))]
[JsonSerializable(typeof(AcmeAuthorizationStatusUpdateRequest))]
[JsonSerializable(typeof(AcmeChallengeType))]
[JsonSerializable(typeof(AcmeChallengeStatus))]
[JsonSerializable(typeof(AcmeChallengeResource))]
[JsonSerializable(typeof(AcmeDirectoryMetadata))]
[JsonSerializable(typeof(AcmeDirectoryResource))]
[JsonSerializable(typeof(AcmeEmptyObject))]
[JsonSerializable(typeof(AcmeExternalAccountProtectedHeader))]
[JsonSerializable(typeof(AcmeFinalizeOrderRequest))]
[JsonSerializable(typeof(AcmeIdentifier))]
[JsonSerializable(typeof(AcmeIdentifierType))]
[JsonSerializable(typeof(AcmeJsonWebKey))]
[JsonSerializable(typeof(AcmeKeyChangeRequest))]
[JsonSerializable(typeof(AcmeNewAccountRequest))]
[JsonSerializable(typeof(AcmeNewAuthorizationRequest))]
[JsonSerializable(typeof(AcmeNewOrderRequest))]
[JsonSerializable(typeof(AcmeOrderListResource))]
[JsonSerializable(typeof(AcmeOrderResource))]
[JsonSerializable(typeof(AcmeOrderStatus))]
[JsonSerializable(typeof(AcmeProblemDetails))]
[JsonSerializable(typeof(AcmeProtectedHeader))]
[JsonSerializable(typeof(AcmeRenewalInfoResource))]
[JsonSerializable(typeof(AcmeRenewalWindow))]
[JsonSerializable(typeof(AcmeRevocationRequest))]
[JsonSerializable(typeof(AcmeSignedMessage))]
[JsonSerializable(typeof(AcmeUpdateAccountRequest))]
[JsonSerializable(typeof(JsonObjectAccountRequest))]
internal sealed partial class AcmeJsonSerializerContext : JsonSerializerContext;
