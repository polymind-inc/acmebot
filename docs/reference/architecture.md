---
description: "Acmebot architecture: how the Function App coordinates ACME orders, DNS-01 validation, Key Vault operations, and ARI-aware scheduled renewal."
---

# Architecture

Acmebot is a Function App that coordinates ACME certificate orders, DNS-01 validation, Key Vault certificate operations, scheduled renewal, and optional notifications.

## Components

| Component | Responsibility |
| --- | --- |
| Dashboard | Browser UI for certificate operations. |
| HTTP API | Authenticated endpoints for certificate and DNS operations. |
| Durable Functions | Long-running issuance, renewal, and revocation orchestration. |
| DNS providers | Create and delete DNS-01 TXT records. |
| ACME client | Communicates with the configured ACME directory. |
| Key Vault certificate client | Creates certificate operations and merges issued certificate chains. |
| ACME state store | Stores ACME account and client state. |
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

The precondition check enumerates DNS zones once, before creating the ACME order. It passes only the relevant `DnsZone` objects to authorization and cleanup activities, including more specific `_acme-challenge` zones. `DnsZone` contains only the zone identifier, name, and name servers, so it can be serialized directly without provider clients or credentials. Both activities reuse this snapshot and select the configured provider by name instead of enumerating zones again. Provider method definitions and record creation and deletion operations are unchanged.

When there are no authorization URLs or all authorizations are already valid, issuance skips DNS writes, propagation waits, TXT checks, challenge answers, and cleanup. It still waits for the order to become ready before finalization. Orders that are immediately ready still run the precondition check first.

Each DNS propagation check queries each distinct TXT record name once, ignoring differences in name casing, and verifies every expected challenge value against that response. Checks do not reuse responses across retries, so later attempts can observe newly propagated records.

## Renewal Flow

The scheduled renewal timer runs daily and starts certificate-level renewal evaluation.

The renewal flow:

1. Lists certificates in the configured Key Vault.
2. Filters to enabled certificates tagged as Acmebot-managed.
3. Filters to the current ACME endpoint.
4. Maintains independent renewal state and next check time for each certificate.
5. Uses ACME Renewal Information (ARI) when available, selecting that certificate's next check from the CA's suggested window and `Retry-After` timing.
6. Renews after the suggested window has started, or falls back to `RenewBeforeExpiry` lifetime-percentage renewal when renewal information is unavailable for the certificate.
7. Reissues each due certificate with its stored Key Vault policy and sends the previous certificate identifier as `replaces` when ARI is available.

For certificates due for renewal, the evaluation activity includes the policy from the certificate it already fetched. Issuance uses this snapshot without reading the certificate again, so changes or deletion after evaluation are not rechecked before issuance starts. Evaluations that do not require renewal omit the policy. Older persisted evaluation results have no policy; the scheduler retains the separate policy retrieval activity for those histories to preserve their call sequence during replay.

Automatic renewal failures put only that certificate into a retrying state. It waits six hours, then evaluates that certificate again.

## State Storage

Acmebot keeps ACME account state in one of two locations, depending on the environment:

| Environment | Store |
| --- | --- |
| Without Azure Files content share | Blob storage container `acmebot-state`. |
| With Azure Files content share | File system under `%HOME%/data/.acmebot/<endpoint-host>/`. |

The v5 deployment template creates the `acmebot-state` container.

Each state store provides an exclusive initialization lock scoped to the endpoint host. The Blob store uses a 60-second lease on `account.lock`, renewed every 20 seconds. The file system store holds `account.lock` open with exclusive sharing on the shared file system. When account state is incomplete, workers acquire the lock and reread state before creating an account. They hold the lock until initialization finishes, so concurrent workers reuse the same account and key. Workers loading a complete account and cached clients do not acquire the lock.

Initialization saves a new account key before registering it with the certificate authority. Key persistence is create-only: the Blob store uses `If-None-Match: *`, and the file system store moves a completed temporary file without overwriting the destination. If another worker has already persisted a key, initialization stops before registration; a retry loads the winning state. This prevents an expired lease owner from overwriting a newer worker's key even if lease-loss cancellation is delayed. If registration or saving account details fails, the next attempt reuses the persisted key to create or recover the same account. Losing or failing to renew a Blob lease cancels initialization. The lock coordinates participating workers; it does not make writes to the separate state files transactional.

File system locking depends on the operating system and mount configuration. The exclusive file handle has been tested on local Windows storage; exclusion across hosts on mounted Azure Files has not been verified. On Linux, `FileShare.None` uses advisory locking, whose propagation across SMB mounts depends on the kernel and mount options. The file system store currently uses file locking rather than Blob leases.

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
- Optional app roles can restrict issue, manual renewal, and revoke operations.
- Key Vault access is handled through Azure identity and RBAC.
- DNS provider secrets are app settings and should be scoped and rotated like other operational credentials.
