# Architecture

Acmebot is an Azure Functions application that coordinates ACME certificate orders, DNS-01 validation, Azure Key Vault certificate operations, scheduled renewal, and optional notifications.

## Components

| Component | Responsibility |
| --- | --- |
| Dashboard | Browser UI for certificate operations. |
| HTTP API | Authenticated endpoints for certificate and DNS operations. |
| Durable Functions | Long-running issuance, renewal, and revocation orchestration. |
| DNS providers | Create and delete DNS-01 TXT records. |
| ACME client | Talks to the configured ACME directory. |
| Key Vault certificate client | Creates certificate operations and merges issued certificate chains. |
| Acme state store | Stores ACME account and client state. |
| Webhook invoker | Sends operation notifications. |

## Issuance Flow

1. The dashboard posts a certificate policy to `POST /api/certificates`.
2. The HTTP function validates authentication, authorization, and request shape.
3. A Durable Functions orchestration starts.
4. Acmebot checks that requested DNS names map to configured DNS zones.
5. Acmebot creates an ACME order.
6. Acmebot creates DNS-01 TXT records through the selected provider.
7. Acmebot waits for provider-specific propagation.
8. Acmebot queries DNS for the expected TXT values.
9. Acmebot answers ACME challenges and waits for the order to become ready.
10. Key Vault creates the certificate operation and CSR.
11. Acmebot finalizes the ACME order with the Key Vault CSR.
12. Acmebot downloads the issued chain and merges it into Key Vault.
13. Acmebot stores metadata tags and sends a completion webhook.

DNS records are always cleaned up after challenge processing, even when an operation fails.

## Renewal Flow

The scheduled renewal timer runs daily and starts a renewal orchestrator.

The orchestrator:

1. Lists certificates in the configured Key Vault.
2. Filters to certificates tagged as Acmebot-managed.
3. Filters to the current ACME endpoint.
4. Uses ACME renewal information when available for the certificate.
5. Falls back to `RenewBeforeExpiry` lifetime-percentage renewal when renewal information is unavailable for the certificate.
6. Adds random jitter up to 600 seconds.
7. Reissues each due certificate with its stored Key Vault policy.

Persistent DNS-related ACME validation errors can be retried by the renewal workflow. Other failures are logged and the orchestrator continues with the next due certificate.

## State Storage

Acmebot keeps ACME account state in one of two locations, depending on the environment:

| Environment | Store |
| --- | --- |
| Without Azure Files content share | Blob storage container `acmebot-state`. |
| With Azure Files content share | File system under `%HOME%/data/.acmebot/<endpoint-host>/`. |

The v5 template creates the `acmebot-state` container.

## Identity

Azure resource access uses managed identity. Acmebot is designed to run in Azure and does not fall back to developer credentials such as Azure CLI or Visual Studio sign-ins.

By default, this uses the Function App system-assigned managed identity. `Acmebot__ManagedIdentityClientId` selects the app-wide user-assigned managed identity for Key Vault certificate operations, Key Vault keys, Azure DNS providers that do not override it, Route 53 web identity federation when `RoleArn` is set, and Google Cloud DNS workload identity federation when `KeyFile64` is empty.

Providers can select their own user-assigned managed identity through provider-specific settings:

- `Acmebot__AzureDns__ManagedIdentityClientId`
- `Acmebot__AzurePrivateDns__ManagedIdentityClientId`
- `Acmebot__GoogleDns__ManagedIdentityClientId`
- `Acmebot__Route53__ManagedIdentityClientId`

When a provider-specific client ID is empty, that provider uses the app-wide managed identity from `Acmebot__ManagedIdentityClientId`. If the app-wide client ID is also empty, Azure SDK clients use the system-assigned managed identity. TransIP request signing uses the Key Vault identity because its private key is stored in Key Vault.

## Key Vault Metadata

Acmebot stores internal metadata in the `Acmebot` certificate tag as JSON. The metadata includes:

- ACME endpoint host.
- DNS provider name.
- Optional DNS alias.
- ACME certificate identifier used for renewal information.

Older certificates with legacy tags are still read for compatibility.

## DNS Zone Matching

Acmebot asks each configured provider for zones, then finds the matching zone for each requested DNS name.

If a provider returns name servers, Acmebot checks that public DNS NS responses intersect with the provider's expected name servers. This catches common cases where a zone exists in the provider account but the domain is not delegated to it.

## Timers

| Function | Schedule | Purpose |
| --- | --- | --- |
| `RenewCertificates_Timer` | Daily at midnight UTC | Starts scheduled renewal. |
| `PurgeInstanceHistory_Timer` | Monthly on day 1 at midnight UTC | Purges completed and failed Durable Functions history older than one month. |

## Observability

The Function App uses OpenTelemetry and Azure Monitor export. The deployment template creates Application Insights connected to Log Analytics.

Monitor:

- Orchestration failures.
- ACME validation problem details.
- DNS provider HTTP failures.
- Key Vault request failures.
- Webhook delivery warnings.

## Security Boundaries

- The Function App HTTP triggers use anonymous trigger authorization but application code requires an authenticated user.
- Dashboard access should be enforced with App Service Authentication.
- Optional app roles can restrict issue and revoke operations.
- Key Vault access is handled through Azure identity and RBAC.
- DNS provider secrets are app settings and should be scoped and rotated like other operational credentials.
