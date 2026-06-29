---
description: "Day-to-day operation of Acmebot: issuing, ARI-aware renewal scheduling, and revoking ACME certificates in Azure Key Vault after deployment."
---

# Operations

This page covers day-to-day operation after Acmebot has been deployed.

## Scheduled Renewal

The `RenewCertificates` timer runs once per day. It lists Key Vault certificates and starts renewal schedulers for certificates that:

- Are enabled.
- Are tagged as issued by Acmebot.
- Match the currently configured ACME endpoint.

Each managed certificate has its own renewal state, next check time, and retry behavior. A certificate that is waiting, retrying, or falling back to an expiry-based policy does not reset the renewal schedule for other certificates.

Acmebot supports ACME Renewal Information (ARI). When the CA provides renewal information for a certificate, Acmebot follows the certificate authority's `suggestedWindow` when choosing the next check. It selects a random time inside that window, but checks renewal information again first when `Retry-After` is earlier. When a renewal evaluation runs after the suggested window starts, Acmebot renews the certificate and marks the new order as replacing the previous certificate when the CA supports it.

When renewal information is unavailable for a certificate, only that certificate falls back to `Acmebot__RenewBeforeExpiry`.

The default fallback renewal threshold is 30% of the certificate lifetime:

```text
Acmebot__RenewBeforeExpiry=30
```

For example, a 90-day certificate is renewed when about 27 days remain. Use a larger value when downstream distribution takes longer or your organization requires a longer safety margin.

No separate pre-processing delay is added before due certificates are renewed.

If automatic renewal fails, that certificate records a retrying state and checks again after six hours.

## Durable Functions History

The `PurgeInstanceHistory` timer runs monthly and deletes completed or failed Durable Functions history older than one month, so the storage account does not accumulate old orchestration history.

## Monitoring

Use Application Insights and Log Analytics to watch for:

- Function invocation failures.
- Durable orchestration failures.
- Outbound HTTP failures to ACME, DNS providers, Key Vault, or webhook endpoints.
- Webhook delivery warnings.
- Repeated DNS propagation retry messages.

Function execution logs, dependency telemetry, and exception telemetry are the most useful categories.

## Webhook Notifications

Set `Acmebot__Webhook` to receive notification events:

```text
Acmebot__Webhook=https://example.com/webhook
```

The payload format is selected from the webhook host:

| Host | Payload |
| --- | --- |
| `hooks.slack.com` | Slack attachment payload. |
| `.logic.azure.com` | Teams-style Adaptive Card payload. |
| `.environment.api.powerplatform.com` | Teams-style Adaptive Card payload. |
| Other hosts | Generic JSON payload. |

Webhook failures are logged as warnings and never roll back certificate issuance.

## Secret Rotation

To rotate DNS provider credentials:

1. Create the new credential in the DNS provider.
2. Update the Function App app setting.
3. Restart the Function App.
4. Load DNS zones from the dashboard to confirm the new credential works.
5. Revoke the old credential.

For Azure DNS and Key Vault, rotate by updating RBAC assignments or switching to a new user-assigned managed identity.

## Common Failures

The most frequent operational issues are:

- **Dashboard returns 401**: App Service Authentication is not configured or does not require sign-in.
- **DNS zones do not load**: provider app settings or permissions are invalid; for Azure DNS, the managed identity may lack DNS access in the zone subscription.
- **Name server precondition fails**: the domain is not delegated to the provider's name servers.
- **TXT record is not found**: propagation is slow, or the validation design requires an internal resolver (`Acmebot__UseSystemNameServer=true`).
- **ACME order becomes invalid**: review the ACME problem details; persistent configuration errors require a new operation after the DNS issue is fixed.
- **Certificate is not renewed**: the certificate is disabled, lacks Acmebot metadata, was issued for a different endpoint, is outside the renewal window, or is not readable by the Function App identity.
- **Endpoint still serves the old certificate**: the certificate is current in Key Vault, but the consuming Azure service has not synced it.

See [Troubleshooting](./troubleshooting) for the full decision tree, and [Azure Service Integration](./service-integration) for downstream sync behavior.
