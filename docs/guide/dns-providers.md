# DNS Providers

Acmebot uses ACME DNS-01 validation. Every certificate operation creates one or more `_acme-challenge` TXT records, waits for propagation, asks the ACME server to validate them, and then deletes the records.

Configure at least one DNS provider under the `Acmebot` configuration section before starting the Function App.

## Provider Selection

When a certificate is issued, Acmebot lists zones from the configured providers and finds the most specific zone that matches each requested DNS name.

If a certificate request does not specify `dnsProviderName`, Acmebot can infer the provider only when all requested names resolve to zones from a single provider. If multiple providers match, choose the provider explicitly in the dashboard.

## Credential Storage

External DNS provider credentials are Function App app settings. Treat every API key, secret, token, and private key reference as production secret material.

Recommended practice:

- Use provider API tokens instead of account-wide credentials when the provider supports scoped tokens.
- Scope credentials to the exact zones Acmebot manages.
- Store secret values in Key Vault and use App Service Key Vault references when your operations model allows it.
- Restart the Function App after rotating provider credentials.
- Load DNS zones from the dashboard after rotation to confirm the new credential works before revoking the old one.

Reference: [Use Key Vault references in App Service and Azure Functions](https://learn.microsoft.com/azure/app-service/app-service-key-vault-references)

## Supported Providers

| Provider | App setting section | Required settings | Propagation delay |
| --- | --- | --- | --- |
| Akamai Edge DNS | `Acmebot__Akamai` | `Host`, `ClientToken`, `ClientSecret`, `AccessToken` | 120 seconds |
| Azure DNS | `Acmebot__AzureDns` | `SubscriptionId` | 10 seconds |
| Azure Private DNS | `Acmebot__AzurePrivateDns` | `SubscriptionId` | 10 seconds |
| Cloudflare | `Acmebot__Cloudflare` | `ApiToken` | 10 seconds |
| Custom DNS | `Acmebot__CustomDns` | `Endpoint`, `ApiKey` | `PropagationSeconds`, default 180 seconds |
| DNS Made Easy | `Acmebot__DnsMadeEasy` | `ApiKey`, `SecretKey` | 30 seconds |
| Gandi LiveDNS | `Acmebot__GandiLiveDns` | `ApiKey` | 300 seconds |
| GoDaddy | `Acmebot__GoDaddy` | `ApiKey`, `ApiSecret` | 600 seconds |
| Google Cloud DNS | `Acmebot__GoogleDns` | `KeyFile64` or `ProjectId`, `PoolProvider`, `ServiceAccount` | 60 seconds |
| IONOS DNS | `Acmebot__IonosDns` | `ApiKey` | 120 seconds |
| OVH | `Acmebot__Ovh` | `ApplicationKey`, `ApplicationSecret`, `ConsumerKey` | 60 seconds |
| PowerDNS | `Acmebot__PowerDns` | `Endpoint`, `ApiKey` | 30 seconds |
| Regfish | `Acmebot__Regfish` | `ApiKey` | 30 seconds |
| Amazon Route 53 | `Acmebot__Route53` | `RoleArn` or `AccessKey`, `SecretKey` | 10 seconds |
| TransIP DNS | `Acmebot__TransIp` | `CustomerName`, `PrivateKeyName` | 360 seconds |
| UnitedDomains | `Acmebot__UnitedDomains` | `ApiKey` | 60 seconds |

Propagation delay is the initial wait before Acmebot begins querying DNS for the expected TXT record. After that wait, Acmebot retries DNS checks until the record becomes visible.

## Akamai Edge DNS

Use Akamai EdgeGrid credentials that can list primary DNS zones and manage DNS records.

| Option | Description |
| --- | --- |
| `Host` | EdgeGrid API host name, without `https://`. Acmebot calls `https://<host>/config-dns/v2/`. |
| `ClientToken` | EdgeGrid client token from the Akamai API client credentials. |
| `ClientSecret` | EdgeGrid client secret paired with the client token. |
| `AccessToken` | EdgeGrid access token for the API client. |

```text
Acmebot__Akamai__Host=akab-xxxxxxxxxxxxxxxx-xxxxxxxxxxxxxxxx.luna.akamaiapis.net
Acmebot__Akamai__ClientToken=<client-token>
Acmebot__Akamai__ClientSecret=<client-secret>
Acmebot__Akamai__AccessToken=<access-token>
```

## Azure DNS

Azure DNS uses the app-wide managed identity by default. For manual app setting configuration, set `ManagedIdentityClientId` to override it.

| Option | Description |
| --- | --- |
| `SubscriptionId` | Azure subscription ID that contains the public DNS zones Acmebot manages. This can differ from the Function App subscription. |
| `ManagedIdentityClientId` | Optional client ID of a user-assigned managed identity assigned to the Function App for Azure DNS. Leave empty to use the app-wide managed identity. |

```text
Acmebot__AzureDns__SubscriptionId=<subscription-id>
Acmebot__AzureDns__ManagedIdentityClientId=<user-assigned-client-id>
```

Assign the identity a role that can list DNS zones and manage TXT records, such as `DNS Zone Contributor`, on the DNS zone or a tightly scoped resource group.

If the DNS zone is in a different subscription than the Function App, set `SubscriptionId` to the DNS zone subscription and assign the identity in that subscription.

## Azure Private DNS

Azure Private DNS also uses the app-wide managed identity by default. For manual app setting configuration, set `ManagedIdentityClientId` to override it.

| Option | Description |
| --- | --- |
| `SubscriptionId` | Azure subscription ID that contains the private DNS zones Acmebot manages. |
| `ManagedIdentityClientId` | Optional client ID of a user-assigned managed identity assigned to the Function App for Azure Private DNS. Leave empty to use the app-wide managed identity. |

```text
Acmebot__AzurePrivateDns__SubscriptionId=<subscription-id>
Acmebot__AzurePrivateDns__ManagedIdentityClientId=<user-assigned-client-id>
```

Assign `Private DNS Zone Contributor` on the private DNS zone or a tightly scoped resource group.

Private DNS validation only works when the certificate authority can resolve the delegated validation name as required by your DNS design. For public certificates, prefer public DNS validation unless you intentionally delegate `_acme-challenge` to a public validation zone.

## Cloudflare

Use a Cloudflare API token that can read zones and edit DNS records for the target zones.

| Option | Description |
| --- | --- |
| `ApiToken` | Cloudflare API token sent as a bearer token. Grant `Zone:Read` and `DNS:Edit` permissions for the target zones. |

```text
Acmebot__Cloudflare__ApiToken=<api-token>
```

Scope the token to the exact zones Acmebot manages when possible.

## Custom DNS

Use Custom DNS when your DNS platform is not directly supported or when you want to front an internal DNS automation service.

| Option | Description |
| --- | --- |
| `Endpoint` | Base URL of the custom DNS API. The deployment form requires an HTTPS URL. |
| `ApiKey` | API key sent to the custom DNS API. |
| `ApiKeyHeaderName` | HTTP header name used to send `ApiKey`. Defaults to `X-Api-Key`. |
| `PropagationSeconds` | Number of seconds Acmebot waits after writing records before DNS verification starts. Defaults to `180`. |

```text
Acmebot__CustomDns__Endpoint=https://dns-api.example.com/
Acmebot__CustomDns__ApiKey=<api-key>
Acmebot__CustomDns__ApiKeyHeaderName=X-Api-Key
Acmebot__CustomDns__PropagationSeconds=180
```

The endpoint must implement this contract:

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/zones` | Return zones Acmebot can manage. |
| `PUT` | `/zones/{zoneId}/records/{recordName}` | Create or replace a TXT record. |
| `DELETE` | `/zones/{zoneId}/records/{recordName}` | Delete the TXT record. |

`GET /zones` returns an array:

```json
[
  {
    "id": "example.com",
    "name": "example.com",
    "nameServers": ["ns1.example.net", "ns2.example.net"]
  }
]
```

`PUT` receives:

```json
{
  "type": "TXT",
  "ttl": 60,
  "values": ["challenge-value"]
}
```

## DNS Made Easy

Use DNS Made Easy API credentials that can list managed domains and edit TXT records.

| Option | Description |
| --- | --- |
| `ApiKey` | DNS Made Easy API key. Acmebot sends it in the `x-dnsme-apiKey` header. |
| `SecretKey` | DNS Made Easy secret key used to sign API requests. |

```text
Acmebot__DnsMadeEasy__ApiKey=<api-key>
Acmebot__DnsMadeEasy__SecretKey=<secret-key>
```

## Gandi LiveDNS

Use a Gandi LiveDNS API key with access to the domains Acmebot should manage.

| Option | Description |
| --- | --- |
| `ApiKey` | Gandi LiveDNS API key sent as a bearer token to the Gandi v5 API. |

```text
Acmebot__GandiLiveDns__ApiKey=<api-key>
```

## GoDaddy

Use GoDaddy production API credentials that can list domains and manage DNS records.

| Option | Description |
| --- | --- |
| `ApiKey` | GoDaddy API key. |
| `ApiSecret` | GoDaddy API secret. Acmebot sends `ApiKey:ApiSecret` with the `sso-key` authentication scheme. |

```text
Acmebot__GoDaddy__ApiKey=<api-key>
Acmebot__GoDaddy__ApiSecret=<api-secret>
```

GoDaddy propagation can be slower than many other providers, so Acmebot waits 600 seconds before DNS verification.

Some GoDaddy accounts are not entitled to production API access even when credentials can be created. If zone listing or record updates fail despite correct-looking settings, confirm API availability for the account with GoDaddy.

## Google Cloud DNS

Google Cloud DNS can use either a base64-encoded service account JSON key file, or Google Cloud workload identity federation with the Function App managed identity. When `KeyFile64` is set, Acmebot uses the service account key path.

| Option | Description |
| --- | --- |
| `KeyFile64` | Base64-encoded Google service account key JSON. The service account must have Cloud DNS read/write permissions for the target project and zones. |
| `ProjectId` | Google Cloud project ID. Required for workload identity federation. Optional with `KeyFile64` to override the project ID from the key file. |
| `PoolProvider` | Workload identity provider resource name without the leading `//iam.googleapis.com/` prefix, for example `projects/123456789/locations/global/workloadIdentityPools/acmebot/providers/azure`. |
| `ServiceAccount` | Google service account email or unique ID that Acmebot impersonates for Cloud DNS operations. |
| `ManagedIdentityClientId` | Optional client ID of a user-assigned managed identity assigned to the Function App for Google Cloud DNS workload identity federation. Leave empty to use the app-wide managed identity. |

For a service account key:

```text
Acmebot__GoogleDns__KeyFile64=<base64-encoded-service-account-json>
```

Acmebot decodes the value at startup and creates a Google DNS client with the `ndev.clouddns.readwrite` scope.

Service account key setup checklist:

1. Create a Google service account in the project that owns the managed zone.
2. Grant Cloud DNS permissions that allow managed zone listing and DNS record changes.
3. Download a JSON key file for the service account.
4. Base64-encode the full JSON file contents.
5. Store the encoded value in `Acmebot__GoogleDns__KeyFile64`.

For workload identity federation:

```text
Acmebot__GoogleDns__ProjectId=<gcp-project-id>
Acmebot__GoogleDns__PoolProvider=projects/123456789/locations/global/workloadIdentityPools/acmebot/providers/azure
Acmebot__GoogleDns__ServiceAccount=acmebot-dns@<gcp-project-id>.iam.gserviceaccount.com
Acmebot__GoogleDns__ManagedIdentityClientId=
```

Workload identity federation setup checklist:

1. Configure a Google workload identity pool provider that trusts the Function App managed identity token.
2. Allow the workload identity principal to impersonate the Google service account.
3. Grant the Google service account Cloud DNS permissions that allow managed zone listing and DNS record changes.
4. Set `PoolProvider` to the provider resource name without `//iam.googleapis.com/`; Acmebot adds that prefix when building the Google STS audience.
5. Leave `ManagedIdentityClientId` empty to use `Acmebot__ManagedIdentityClientId`, or set it to a user-assigned managed identity client ID assigned to the Function App.

Acmebot requests the Azure managed identity token for `https://management.azure.com/` and uses that token as the subject token for Google STS.

The OAuth scope used by Acmebot is:

```text
https://www.googleapis.com/auth/ndev.clouddns.readwrite
```

## IONOS DNS

Use an IONOS DNS API key that can list zones and manage DNS records.

| Option | Description |
| --- | --- |
| `ApiKey` | IONOS DNS API key sent in the `X-API-Key` header. |

```text
Acmebot__IonosDns__ApiKey=<api-key>
```

## OVH

OVH uses signed API requests with an application key, application secret, and consumer key.

| Option | Description |
| --- | --- |
| `Endpoint` | OVH API endpoint. Defaults to `https://eu.api.ovh.com/1.0/`. Use the endpoint that matches your OVH region. |
| `ApplicationKey` | OVH application key. |
| `ApplicationSecret` | OVH application secret paired with the application key. |
| `ConsumerKey` | OVH consumer key authorized for DNS zone record operations. |

```text
Acmebot__Ovh__Endpoint=https://eu.api.ovh.com/1.0/
Acmebot__Ovh__ApplicationKey=<application-key>
Acmebot__Ovh__ApplicationSecret=<application-secret>
Acmebot__Ovh__ConsumerKey=<consumer-key>
```

Acmebot refreshes the OVH zone after record mutations so changes are published.

## PowerDNS

Use PowerDNS when you operate an authoritative PowerDNS server with the HTTP API enabled.

| Option | Description |
| --- | --- |
| `Endpoint` | Full base URL of the PowerDNS HTTP API, including `/api/v1/`, for example `https://pdns.example.com/api/v1/`. |
| `ApiKey` | PowerDNS HTTP API key sent in the `X-API-Key` header. |
| `ServerId` | PowerDNS server identifier used in paths under `/servers/{serverId}`. Defaults to `localhost`. |

```text
Acmebot__PowerDns__Endpoint=https://pdns.example.com/api/v1/
Acmebot__PowerDns__ApiKey=<api-key>
Acmebot__PowerDns__ServerId=localhost
```

## Regfish

Use a Regfish API key that can list DNS zones and manage DNS records.

| Option | Description |
| --- | --- |
| `ApiKey` | Regfish API key sent in the `x-api-key` header. |

```text
Acmebot__Regfish__ApiKey=<api-key>
```

Regfish can return transient server errors when listing records for otherwise usable zones. Acmebot handles known empty-list cases, but persistent failures should be checked in Application Insights.

## Amazon Route 53

Route 53 can use either an AWS IAM role assumed with the Function App managed identity, or static AWS access key credentials. When `RoleArn` is set, Acmebot uses STS `AssumeRoleWithWebIdentity` and ignores `AccessKey` and `SecretKey`.

| Option | Description |
| --- | --- |
| `RoleArn` | AWS IAM role ARN assumed with STS `AssumeRoleWithWebIdentity` using the selected Azure managed identity. |
| `ManagedIdentityClientId` | Optional client ID of a user-assigned managed identity assigned to the Function App for Route 53 web identity federation. Leave empty to use the app-wide managed identity. |
| `AccessKey` | AWS access key ID used when `RoleArn` is empty. |
| `SecretKey` | AWS secret access key paired with `AccessKey` when `RoleArn` is empty. |

```text
Acmebot__Route53__RoleArn=arn:aws:iam::123456789012:role/acmebot-route53
Acmebot__Route53__ManagedIdentityClientId=
```

For static AWS credentials instead:

```text
Acmebot__Route53__AccessKey=<access-key>
Acmebot__Route53__SecretKey=<secret-key>
```

Acmebot lists public hosted zones and creates TXT records in the matching hosted zone.

The IAM role or access key needs these minimum permissions:

- `route53:ListHostedZones`
- `route53:ListResourceRecordSets`
- `route53:ChangeResourceRecordSets`

Example IAM policy scoped to one hosted zone:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowZoneRecordChanges",
      "Effect": "Allow",
      "Action": [
        "route53:ChangeResourceRecordSets",
        "route53:ListResourceRecordSets"
      ],
      "Resource": "arn:aws:route53:::hostedzone/YOUR_ZONE_ID"
    },
    {
      "Sid": "AllowHostedZoneListing",
      "Effect": "Allow",
      "Action": "route53:ListHostedZones",
      "Resource": "*"
    }
  ]
}
```

## TransIP DNS

TransIP uses a customer name and a private key stored as an Azure Key Vault key.

| Option | Description |
| --- | --- |
| `CustomerName` | TransIP customer name used to request API access tokens. |
| `PrivateKeyName` | Name of the Azure Key Vault key that contains the TransIP private key. Acmebot looks under `Acmebot__VaultBaseUrl` at `/keys/{PrivateKeyName}` and signs requests with that key. |

```text
Acmebot__TransIp__CustomerName=<customer-name>
Acmebot__TransIp__PrivateKeyName=<key-name>
```

The app-wide managed identity must be allowed to use the Key Vault key for signing.

## UnitedDomains

Use a UnitedDomains API key that can list zones and manage DNS records.

| Option | Description |
| --- | --- |
| `ApiKey` | UnitedDomains API key sent in the `X-API-Key` header. |

```text
Acmebot__UnitedDomains__ApiKey=<api-key>
```

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No DNS zones appear in the dashboard | Verify provider credentials and that the provider can list zones. |
| `No DNS zone was found` | Confirm the requested DNS name is under a configured zone or use `dnsAlias`. |
| Delegated name server error | Confirm the authoritative NS records match the provider's zone name servers. |
| TXT record not found | Increase provider propagation delay, check resolver choice, and verify the record exists at `_acme-challenge`. |
| Multiple providers match | Select the provider explicitly in the dashboard. |
