# Operations

This page covers day-to-day operations after Acmebot is deployed.

## Scheduled Renewal

The `RenewCertificates` timer runs once per day. It lists Key Vault certificates and selects certificates that:

- Are tagged as issued by Acmebot.
- Match the currently configured ACME endpoint.
- Are inside the ACME renewal information window, when the CA supports renewal information.
- Or expire within `Acmebot__RenewBeforeExpiry` days.

The default renewal window is 30 days:

```text
Acmebot__RenewBeforeExpiry=30
```

Set a larger value when downstream distribution takes longer or when your organization requires a longer safety window.

## Renewal Jitter

Before processing due certificates, the renewal orchestrator waits for a random delay of up to 600 seconds. This reduces the chance that many deployments start renewal at exactly the same time.

## Durable Functions History

The `PurgeInstanceHistory` timer runs monthly and deletes completed or failed Durable Functions history older than one month.

This keeps the storage account from accumulating old orchestration history indefinitely.

## Monitoring

Use Application Insights and Log Analytics to monitor:

- Function invocation failures.
- Durable orchestration failures.
- Outbound HTTP failures to ACME, DNS providers, Key Vault, or webhook endpoints.
- Webhook delivery warnings.
- Repeated DNS propagation retry messages.

Useful log categories include Function execution logs, dependency telemetry, and exception telemetry.

## Webhook Notifications

Set `Acmebot__Webhook` to receive notification events.

```text
Acmebot__Webhook=https://example.com/webhook
```

Payload format is selected from the webhook host:

| Host | Payload |
| --- | --- |
| `hooks.slack.com` | Slack attachment payload. |
| `.logic.azure.com` | Teams-style Adaptive Card payload. |
| `.environment.api.powerplatform.com` | Teams-style Adaptive Card payload. |
| Other hosts | Generic JSON payload. |

Webhook failures are logged as warnings and do not roll back certificate issuance.

## Common Failures

For a fuller decision tree, see [Troubleshooting](./troubleshooting).

### Dashboard Returns 401

Check App Service Authentication. The dashboard and API expect authenticated requests.

### DNS Zones Do Not Load

Check the selected DNS provider app settings and permissions. For Azure DNS, confirm the managed identity has DNS zone permissions in the DNS zone subscription.

### Name Server Precondition Fails

For providers that expose name servers, Acmebot compares the zone's expected name servers with public DNS NS responses. Confirm the domain is delegated to the provider's name servers.

### TXT Record Is Not Found

Check whether the record was created at `_acme-challenge.<name>`. If your environment requires internal DNS resolvers, set `Acmebot__UseSystemNameServer=true`.

### ACME Order Becomes Invalid

Review the ACME problem details in Application Insights. DNS-related ACME validation errors are retried automatically when possible, but persistent configuration errors require a new operation after the DNS issue is fixed.

### Certificate Is Not Renewed

Confirm the certificate:

- Has Acmebot metadata in Key Vault.
- Was issued for the currently configured endpoint.
- Is within the renewal window.
- Is enabled and readable by the Function App identity.

### Endpoint Still Serves The Old Certificate

If the renewed certificate is current in Key Vault, check the consuming Azure service. App Service, Front Door, Application Gateway, API Management, Container Apps, SignalR Service, and VM-based workloads each have their own sync or import behavior.

Use [Azure Service Integration](./service-integration) to verify the downstream certificate reference.

## Secret Rotation

When rotating DNS provider credentials:

1. Create the new credential in the DNS provider.
2. Update the Function App app setting.
3. Restart the Function App.
4. Load DNS zones from the dashboard to confirm the new credential works.
5. Revoke the old credential.

For Azure DNS and Key Vault, rotate by updating RBAC assignments or switching to a new user-assigned managed identity.
