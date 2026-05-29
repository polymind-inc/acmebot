# Operations

This page covers day-to-day operation after Acmebot is deployed.

## Scheduled Renewal

The `RenewCertificates` timer runs once per day. It lists Key Vault certificates and selects those that:

- Are tagged as issued by Acmebot.
- Match the currently configured ACME endpoint.
- Are inside the ACME renewal information window, when the CA supports it.
- Or expire within `Acmebot__RenewBeforeExpiry` days.

The default renewal window is 30 days:

```text
Acmebot__RenewBeforeExpiry=30
```

Use a larger value when downstream distribution takes longer or your organization requires a longer safety margin.

## Renewal Jitter

Before processing due certificates, the renewal orchestrator waits a random delay of up to 600 seconds. This prevents many deployments from starting renewal at exactly the same time.

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

- **Dashboard returns 401** — App Service Authentication is not configured or not requiring sign-in.
- **DNS zones do not load** — provider app settings or permissions are wrong; for Azure DNS, the managed identity lacks DNS access in the zone subscription.
- **Name server precondition fails** — the domain is not delegated to the provider's name servers.
- **TXT record is not found** — propagation is slow, or an internal resolver is needed (`Acmebot__UseSystemNameServer=true`).
- **ACME order becomes invalid** — review the ACME problem details; persistent configuration errors need a new operation after the DNS issue is fixed.
- **Certificate is not renewed** — the certificate lacks Acmebot metadata, was issued for a different endpoint, is outside the renewal window, or is not readable by the Function App identity.
- **Endpoint still serves the old certificate** — the certificate is current in Key Vault but the consuming Azure service has not synced it.

See [Troubleshooting](./troubleshooting) for the full decision tree, and [Azure Service Integration](./service-integration) for downstream sync behavior.
