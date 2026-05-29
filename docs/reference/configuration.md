# Configuration

Acmebot uses .NET configuration binding for the `Acmebot` section. In Azure App Service and Azure Functions app settings, nested settings are expressed with double underscores.

Example:

```text
Acmebot__Endpoint=https://acme-v02.api.letsencrypt.org/directory
```

## Required Settings

| Setting | Description |
| --- | --- |
| `Acmebot__Endpoint` | ACME directory endpoint. |
| `Acmebot__Contacts` | ACME account contacts, such as `mailto:admin@example.com`. |
| `Acmebot__VaultBaseUrl` | Key Vault URL where certificates are stored. |
| `Acmebot__Environment` | Azure cloud name. Defaults to `AzureCloud`. |

## General Settings

| Setting | Default | Description |
| --- | --- | --- |
| `Acmebot__Webhook` | Empty | Webhook URL for certificate operation notifications. |
| `Acmebot__PreferredChain` | Empty | Preferred issuer chain name when the ACME CA offers alternate chains. |
| `Acmebot__PreferredProfile` | Empty | Preferred ACME profile when the CA advertises profiles. |
| `Acmebot__RenewBeforeExpiry` | `30` | Number of days before certificate expiry when scheduled renewal should run. Valid range is 0 to 365. |
| `Acmebot__UseSystemNameServer` | `false` | Use the system DNS resolver instead of Google Public DNS for challenge verification. |
| `Acmebot__ManagedIdentityClientId` | Empty | Client ID for the app-wide user-assigned managed identity used for Key Vault certificate operations, Key Vault keys, Azure DNS providers that do not override it, Route 53 web identity federation when `RoleArn` is set, and Google Cloud DNS workload identity federation when `KeyFile64` is empty. When empty, Acmebot uses the system-assigned managed identity. The user-assigned identity must be assigned to the Function App. |

## Azure Environments

| Value | Cloud |
| --- | --- |
| `AzureCloud` | Azure Public |
| `AzureChinaCloud` | Azure China |
| `AzureUSGovernment` | Azure Government |

The selected environment controls Azure Resource Manager and identity authority hosts.

## External Account Binding

Configure these settings before first ACME account registration when the selected CA requires EAB.

| Setting | Default | Description |
| --- | --- | --- |
| `Acmebot__ExternalAccountBinding__KeyId` | Empty | EAB key identifier. |
| `Acmebot__ExternalAccountBinding__HmacKey` | Empty | EAB HMAC key in base64url format. |
| `Acmebot__ExternalAccountBinding__Algorithm` | `HS256` | EAB HMAC signing algorithm. Common values are `HS256`, `HS384`, and `HS512`. |

## DNS Provider Settings

Configure one or more provider sections. Acmebot creates all providers whose option section is present.

Provider credentials are secrets. Use scoped provider tokens where possible, and consider App Service Key Vault references for secret values stored in Function App settings.

### Akamai Edge DNS

| Setting | Description |
| --- | --- |
| `Acmebot__Akamai__Host` | Akamai EdgeGrid API host name, without `https://`. Acmebot calls `https://<host>/config-dns/v2/`. |
| `Acmebot__Akamai__ClientToken` | EdgeGrid client token from the Akamai API client credentials. |
| `Acmebot__Akamai__ClientSecret` | EdgeGrid client secret paired with the client token. |
| `Acmebot__Akamai__AccessToken` | EdgeGrid access token for the API client. |

### Azure DNS

| Setting | Description |
| --- | --- |
| `Acmebot__AzureDns__SubscriptionId` | Azure subscription ID containing the public DNS zones Acmebot manages. The selected identity must have zone read and TXT record write/delete access in this subscription. |
| `Acmebot__AzureDns__ManagedIdentityClientId` | Optional client ID for a user-assigned managed identity used for Azure DNS. When empty, Acmebot uses the app-wide managed identity from `Acmebot__ManagedIdentityClientId`, or the system-assigned managed identity if the app-wide client ID is empty. The user-assigned identity must be assigned to the Function App. |

Azure DNS uses the app-wide managed identity by default and this setting overrides it.

### Azure Private DNS

| Setting | Description |
| --- | --- |
| `Acmebot__AzurePrivateDns__SubscriptionId` | Azure subscription ID containing the private DNS zones Acmebot manages. The selected identity must have private zone read and TXT record write/delete access in this subscription. |
| `Acmebot__AzurePrivateDns__ManagedIdentityClientId` | Optional client ID for a user-assigned managed identity used for Azure Private DNS. When empty, Acmebot uses the app-wide managed identity from `Acmebot__ManagedIdentityClientId`, or the system-assigned managed identity if the app-wide client ID is empty. The user-assigned identity must be assigned to the Function App. |

Azure Private DNS uses the app-wide managed identity by default and this setting overrides it.

### Cloudflare

| Setting | Description |
| --- | --- |
| `Acmebot__Cloudflare__ApiToken` | Cloudflare API token sent as a bearer token. Grant `Zone:Read` and `DNS:Edit` permissions for the target zones. |

### Custom DNS

| Setting | Default | Description |
| --- | --- | --- |
| `Acmebot__CustomDns__Endpoint` | Required | Base URL for the custom DNS API. The API must expose `/zones` and `/zones/{zoneId}/records/{recordName}` endpoints. |
| `Acmebot__CustomDns__ApiKey` | Required | API key sent to the custom DNS API. |
| `Acmebot__CustomDns__ApiKeyHeaderName` | `X-Api-Key` | HTTP header name used to send `ApiKey`. |
| `Acmebot__CustomDns__PropagationSeconds` | `180` | Number of seconds Acmebot waits after writing TXT records before DNS verification starts. |

### DNS Made Easy

| Setting | Description |
| --- | --- |
| `Acmebot__DnsMadeEasy__ApiKey` | DNS Made Easy API key. Acmebot sends it in the `x-dnsme-apiKey` header. |
| `Acmebot__DnsMadeEasy__SecretKey` | DNS Made Easy secret key used to sign API requests. |

### Gandi LiveDNS

| Setting | Description |
| --- | --- |
| `Acmebot__GandiLiveDns__ApiKey` | Gandi LiveDNS API key sent as a bearer token to the Gandi v5 API. |

### GoDaddy

| Setting | Description |
| --- | --- |
| `Acmebot__GoDaddy__ApiKey` | GoDaddy production API key. |
| `Acmebot__GoDaddy__ApiSecret` | GoDaddy production API secret. Acmebot sends `ApiKey:ApiSecret` with the `sso-key` authentication scheme. |

Confirm the account is entitled to GoDaddy production API access if zone listing or record updates fail despite valid credentials.

### Google Cloud DNS

| Setting | Description |
| --- | --- |
| `Acmebot__GoogleDns__KeyFile64` | Base64-encoded Google service account key JSON. The service account must have Cloud DNS read/write permissions for the target project and zones. |
| `Acmebot__GoogleDns__ProjectId` | Google Cloud project ID. Required for workload identity federation. Optional with `KeyFile64` to override the project ID from the key file. |
| `Acmebot__GoogleDns__PoolProvider` | Workload identity provider resource name without the leading `//iam.googleapis.com/` prefix. |
| `Acmebot__GoogleDns__ServiceAccount` | Google service account email or unique ID that Acmebot impersonates for Cloud DNS operations. |
| `Acmebot__GoogleDns__ManagedIdentityClientId` | Optional client ID for a user-assigned managed identity used to obtain the subject token for Google Cloud DNS workload identity federation. When empty, Acmebot uses the app-wide managed identity from `Acmebot__ManagedIdentityClientId`, or the system-assigned managed identity if the app-wide client ID is empty. The user-assigned identity must be assigned to the Function App. |

Acmebot uses the Google Cloud DNS read/write OAuth scope and ignores private managed zones. When `KeyFile64` is set, service account key authentication is used. Otherwise, all of `ProjectId`, `PoolProvider`, and `ServiceAccount` must be set for workload identity federation.

### IONOS DNS

| Setting | Description |
| --- | --- |
| `Acmebot__IonosDns__ApiKey` | IONOS DNS API key sent in the `X-API-Key` header. |

### OVH

| Setting | Default | Description |
| --- | --- | --- |
| `Acmebot__Ovh__Endpoint` | `https://eu.api.ovh.com/1.0/` | OVH API endpoint. Use the endpoint that matches your OVH region. |
| `Acmebot__Ovh__ApplicationKey` | Required | OVH application key. |
| `Acmebot__Ovh__ApplicationSecret` | Required | OVH application secret paired with the application key. |
| `Acmebot__Ovh__ConsumerKey` | Required | OVH consumer key authorized for DNS zone record operations. |

### PowerDNS

| Setting | Default | Description |
| --- | --- | --- |
| `Acmebot__PowerDns__Endpoint` | Required | Full base URL of the PowerDNS HTTP API, including `/api/v1/`, for example `https://pdns.example.com/api/v1/`. |
| `Acmebot__PowerDns__ApiKey` | Required | PowerDNS HTTP API key sent in the `X-API-Key` header. |
| `Acmebot__PowerDns__ServerId` | `localhost` | PowerDNS server identifier used in paths under `/servers/{serverId}`. |

### Regfish

| Setting | Description |
| --- | --- |
| `Acmebot__Regfish__ApiKey` | Regfish API key sent in the `x-api-key` header. |

### Amazon Route 53

| Setting | Description |
| --- | --- |
| `Acmebot__Route53__RoleArn` | AWS IAM role ARN assumed with STS `AssumeRoleWithWebIdentity` using the selected Azure managed identity. When set, `AccessKey` and `SecretKey` are not used. |
| `Acmebot__Route53__ManagedIdentityClientId` | Optional client ID for a user-assigned managed identity used to obtain the web identity token for Route 53. When empty, Acmebot uses the app-wide managed identity from `Acmebot__ManagedIdentityClientId`, or the system-assigned managed identity if the app-wide client ID is empty. The user-assigned identity must be assigned to the Function App. |
| `Acmebot__Route53__AccessKey` | AWS access key ID used by the Route 53 client when `RoleArn` is empty. |
| `Acmebot__Route53__SecretKey` | AWS secret access key paired with `AccessKey` when `RoleArn` is empty. |

The AWS role or access key credentials need permission to list hosted zones, list record sets, and change record sets in the target hosted zone.

### TransIP DNS

| Setting | Description |
| --- | --- |
| `Acmebot__TransIp__CustomerName` | TransIP customer name used to request API access tokens. |
| `Acmebot__TransIp__PrivateKeyName` | Name of the Azure Key Vault key that contains the TransIP private key. Acmebot looks under `Acmebot__VaultBaseUrl` at `/keys/{PrivateKeyName}` and signs requests with that key. |

TransIP signs requests with an Azure Key Vault key under `Acmebot__VaultBaseUrl`.

### UnitedDomains

| Setting | Description |
| --- | --- |
| `Acmebot__UnitedDomains__ApiKey` | UnitedDomains API key sent in the `X-API-Key` header. |

## Dashboard Authorization Setting

Issue and revoke operations can optionally require Microsoft Entra app roles.

| Setting | Default | Description |
| --- | --- | --- |
| `Acmebot__RequireAppRoles` | `false` | When `true`, issue operations require `Acmebot.IssueCertificate` and revoke operations require `Acmebot.RevokeCertificate`. |

This value is read as an environment variable by the authorization helper.

## Platform Settings

The deployment template also configures platform settings such as:

| Setting | Purpose |
| --- | --- |
| `AzureWebJobsStorage` | Function runtime storage and Acmebot state storage connection string. |
| `DEPLOYMENT_STORAGE_CONNECTION_STRING` | Flex Consumption package deployment storage connection string. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights telemetry connection string. |

Do not remove these settings from deployed Function Apps.

## Complete Example

```text
Acmebot__Endpoint=https://acme-v02.api.letsencrypt.org/directory
Acmebot__Contacts=mailto:admin@example.com
Acmebot__VaultBaseUrl=https://my-vault.vault.azure.net/
Acmebot__Environment=AzureCloud
Acmebot__AzureDns__SubscriptionId=00000000-0000-0000-0000-000000000000
Acmebot__AzureDns__ManagedIdentityClientId=
Acmebot__RenewBeforeExpiry=30
Acmebot__Webhook=https://example.com/webhook
```
