# Dashboard

The dashboard is the primary interface for Acmebot. The Function App serves the dashboard, and the dashboard calls the same-origin `/api/*` endpoints.

## Access

The dashboard requires authenticated requests, so configure App Service Authentication on the Function App before using it. Once authentication is enabled, open the Function App URL in a browser. If the dashboard returns `401 Unauthorized`, check the authentication configuration first.

## Certificate List

The dashboard lists certificates from the configured Key Vault and marks each one by category:

- Managed by Acmebot and issued for the current ACME endpoint.
- Managed by Acmebot but issued for another ACME endpoint.
- Not managed by Acmebot.

Only enabled certificates with Acmebot metadata for the current endpoint are selected for scheduled renewal.

## DNS Zone List

The dashboard lists zones from all configured DNS providers. A provider that fails to list zones is dropped from the result, so check Application Insights if an expected provider is missing.

Load the zone list before issuing your first certificate. This confirms that DNS credentials, managed identity permissions, and network access are configured correctly.

## Issue a Certificate

1. Select a DNS provider and zone.
2. Enter the record name under that zone.
3. Review the resulting DNS name.
4. Choose key options.
5. Set advanced options if required.
6. Submit the request and wait for the operation overlay to complete.

Leave the record name empty for the zone apex, or use `*` as the leftmost label for a wildcard certificate.

## Key Options

| Key type | Supported values |
| --- | --- |
| RSA | `2048`, `3072`, `4096` |
| EC | `P-256`, `P-384`, `P-521`, `P-256K` |

The default is RSA 2048. Use EC only when the downstream service supports the selected curve.

## Advanced Options

### Certificate Name

Certificate names can contain letters, numbers, and hyphens. If omitted in the dashboard, the dashboard derives a name from the first DNS name by replacing `*` with `wildcard` and dots with hyphens before sending the request.

### Delegated DNS-01

Use the delegated DNS-01 issue mode when the DNS provider selected in Acmebot manages only the validation zone, not the certificate's public DNS zone. Enter full DNS names for the certificate, select the DNS alias zone that Acmebot can update, and create the displayed CNAME records in the authoritative DNS provider before submitting.

### DNS Alias

Set DNS Alias when the ACME challenge should be created under another domain. Acmebot then writes TXT records at `_acme-challenge.<dnsAlias>` with the selected DNS provider. The alias value must not include the `_acme-challenge` prefix. In delegated DNS-01 mode, the dashboard generates a unique alias record in the selected alias zone and shows the CNAME records to create in the certificate domain's DNS provider.

### Tags

Custom tags are written to the Key Vault certificate. The `Acmebot` tag is reserved for internal metadata and cannot be set manually.

## Renew a Certificate

Manual renewal reuses the existing Key Vault certificate policy and starts a new issuance orchestration. Use it to rotate a certificate before its scheduled renewal window.

Scheduled renewals run daily. Certificates with ACME renewal information use the CA's suggested window and `Retry-After` timing; certificates without renewal information use the configured fallback threshold.

## Revoke a Certificate

Revocation sends the certificate to the configured ACME certificate authority's revoke endpoint. It does not delete the Key Vault certificate. After revocation, decide whether to delete, disable, or replace the certificate version according to your operational policy.

## Operation Status

Issue and renew operations return an operation URL, which the dashboard polls until the operation completes or fails. If an operation fails:

- Read the displayed problem message.
- Check Application Insights for the orchestration instance ID.
- Verify DNS records and provider permissions.
- Retry after fixing the underlying issue.
