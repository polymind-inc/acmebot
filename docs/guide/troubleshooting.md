# Troubleshooting

Use this page when issuance, renewal, revocation, authentication, DNS validation, or downstream Azure service integration does not behave as expected.

## First Checks

Start with the smallest failing unit you can reproduce, and confirm the basics:

- The Function App is running and reachable.
- App Service Authentication is enabled and requiring sign-in.
- The target Key Vault exists and the Function App identity can manage certificates.
- The configured DNS provider can list the zone and edit TXT records.
- Application Insights is connected and receiving Function telemetry.
- You have checked the operation status URL returned by the dashboard or API.

## Where to Look

| Signal | Use it for |
| --- | --- |
| Dashboard operation status | Whether an issue, renew, or revoke request is pending, completed, or failed. |
| `GET /api/operations/{instanceId}` | Programmatic polling of asynchronous operations. |
| Function App log stream | Immediate startup, authentication, and request-level errors. |
| Application Insights exceptions | Durable orchestration, DNS provider, Key Vault, ACME, and webhook failures. |
| Dependency telemetry | Outbound calls to ACME servers, DNS provider APIs, Azure Resource Manager, and Key Vault. |

## DNS Validation Fails

Typical causes:

- The configured DNS provider does not control the public zone being validated.
- The provider credential cannot create or delete `_acme-challenge` TXT records.
- Propagation takes longer than the provider's configured delay.
- The request uses a private or internal DNS zone the CA cannot resolve.
- A delegated validation name is missing or points to the incorrect zone.

What to verify:

- DNS zones load successfully in the dashboard.
- The requested name is under one of the configured zones.
- `dnsProviderName` is set to the DNS provider that owns the validation zone.
- `dnsAlias` has the required CNAME or delegation in place.
- A TXT record appears at `_acme-challenge.<name>` while the operation runs.
- `Acmebot__UseSystemNameServer=true` is used only when the validation design requires the platform resolver.
- For Azure DNS, Azure Private DNS, Route 53 with `RoleArn`, or Google Cloud DNS workload identity federation, the selected identity has the required permissions or trust relationship. A provider-specific managed identity client ID overrides `Acmebot__ManagedIdentityClientId`; when it is empty, verify the app-wide identity has access.

See [DNS Providers](./dns-providers) for provider-specific settings and propagation behavior.

## Key Vault Access Is Denied

Typical causes:

- The Function App identity lacks certificate management permissions.
- The vault permission model changed between Azure RBAC and access policies.
- `Acmebot__VaultBaseUrl` points to the incorrect vault.
- A user-assigned managed identity is intended, but `Acmebot__ManagedIdentityClientId` is missing or incorrect.

What to verify:

- The vault URL in app settings matches the intended Key Vault.
- The Function App identity has `Key Vault Certificates Officer`, or equivalent certificate permissions through access policies.
- For TransIP, the identity can use the Key Vault key named by `Acmebot__TransIp__PrivateKeyName`.
- Downstream services have their own Key Vault read permissions; Acmebot's identity does not grant them access.

## Authentication or Authorization Fails

Typical symptoms:

- The dashboard or an API call returns `401`.
- A signed-in caller can list data but cannot issue or revoke certificates.
- Azure CLI token acquisition fails with `AADSTS65001` or `consent_required`.

What to verify:

- App Service Authentication is configured for the Function App.
- Requests reach the app with an authenticated principal.
- Microsoft Entra ID uses the intended tenant and application registration.
- When `Acmebot__RequireAppRoles=true`, the token contains `Acmebot.IssueCertificate` or `Acmebot.RevokeCertificate`.
- For Azure CLI-backed automation, the application registration used by App Service Authentication exposes a `user_impersonation` scope and pre-authorizes the Microsoft Azure CLI client ID `04b07795-8ddb-461a-bbee-02f9e1bf7b46`.

The HTTP triggers use anonymous trigger authorization so App Service Authentication can populate the user identity before application code runs. A Functions host key alone does not satisfy the authenticated-user requirement for the v5 dashboard and API. See [Security](../reference/security) for the authorization model.

If `az account get-access-token` fails before the HTTP request is sent, fix Microsoft Entra consent first. In the Acmebot application registration, go to **Expose an API** > **Authorized client applications**, add `04b07795-8ddb-461a-bbee-02f9e1bf7b46`, and select `user_impersonation`. If App Service Authentication is configured to allow only specific client applications, add the same client ID to that allow list as well.

## Renewal Does Not Run

Typical causes:

- The certificate was not issued by Acmebot, or no longer has Acmebot metadata.
- The certificate was issued against a different ACME endpoint than the configured one.
- The certificate is not inside the renewal window.
- The timer host is stopped or the Function App is unhealthy.
- DNS or Key Vault permissions changed after first issuance.

What to verify:

- The certificate is enabled and readable in Key Vault and has Acmebot metadata tags.
- `Acmebot__Endpoint` matches the endpoint used when the certificate was issued.
- `Acmebot__RenewBeforeExpiry` is set to the intended remaining lifetime percentage.
- Application Insights shows `RenewCertificates` timer activity.
- No local-time assumption is being applied; Azure Functions timer schedules run in UTC unless the hosting plan supports `WEBSITE_TIME_ZONE`.

## Operation Remains Pending

Certificate operations are Durable Functions orchestrations, so a pending operation can be normal while Acmebot waits for DNS propagation or ACME validation.

If it remains pending longer than expected:

- Check Application Insights for dependency failures.
- Confirm the Function App has not restarted repeatedly.
- Check whether the selected provider has a long propagation delay.
- Poll the operation URL again before starting another operation for the same certificate.

## Certificate Issued But Endpoint Still Uses the Old Certificate

If Key Vault has the new certificate version, ACME issuance succeeded. Continue troubleshooting the consuming Azure service:

- App Service: confirm the imported Key Vault certificate's sync state and binding.
- Front Door: confirm the secret uses `Latest` rather than a pinned version.
- Application Gateway: confirm it references a versionless Key Vault secret and can access Key Vault.
- API Management, SignalR, Container Apps, and VM workloads: verify their service-specific import or sync behavior.

See [Azure Service Integration](./service-integration).

## Logs Are Missing

Typical causes:

- `APPLICATIONINSIGHTS_CONNECTION_STRING` is missing.
- Sampling hides successful requests.
- The log stream is connected to one instance while the app is scaled out.
- The deployment uses a different Log Analytics workspace than expected.

What to verify:

- Application Insights is connected to the Function App.
- Exceptions and dependency telemetry are visible.
- Use Live Metrics for near-real-time, multi-instance checks.

## Triage Workflow

1. Reproduce the problem with one certificate or one API request.
2. Capture the operation URL, HTTP status code, and certificate name.
3. Check Function logs and Application Insights for the failing invocation.
4. Classify the failure domain: authentication, DNS, Key Vault, ACME CA, webhook, or downstream Azure service.
5. Fix permissions or configuration first.
6. Retry the operation and confirm any temporary `_acme-challenge` records are cleaned up.

For routine operational behavior, see [Operations](./operations).
