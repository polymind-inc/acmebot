# Dashboard

The dashboard is the primary user interface for Acmebot. It is served by the Function App and calls the same-origin `/api/*` endpoints.

## Access

The dashboard expects authenticated requests. Configure App Service Authentication on the Function App before using it.

After authentication is enabled, open the Function App URL in a browser. If the dashboard returns `401 Unauthorized`, check the authentication configuration first.

## Certificate List

The dashboard lists certificates from the configured Key Vault. It marks certificates by category:

- Managed by Acmebot and issued for the current ACME endpoint.
- Managed by Acmebot but issued for another ACME endpoint.
- Certificates not managed by Acmebot.

Only certificates with Acmebot metadata for the current endpoint are selected for scheduled renewal.

## DNS Zone List

The dashboard can list zones from all configured DNS providers. A provider that fails to list zones is omitted from the successful result, so check Application Insights if an expected provider is missing.

Use the zone list before issuing the first certificate. It confirms that DNS credentials, Azure managed identity permissions, and network access are working.

## Issue a Certificate

To issue a certificate:

1. Select a DNS provider and zone.
2. Enter the record name under that zone.
3. Review the resulting DNS name.
4. Choose key options.
5. Optionally set advanced options.
6. Submit the request and wait for the operation overlay to complete.

Use an empty record name for the zone apex. Use `*` as the leftmost label for a wildcard certificate.

## Key Options

Supported key options are:

| Key type | Supported values |
| --- | --- |
| RSA | `2048`, `3072`, `4096` |
| EC | `P-256`, `P-384`, `P-521`, `P-256K` |

The default is RSA 2048. Use EC only when the downstream service supports the selected curve.

## Advanced Options

### Certificate Name

Certificate names can contain letters, numbers, and hyphens. If omitted, Acmebot generates a name from the first DNS name by replacing `*` with `wildcard` and dots with hyphens.

### DNS Alias

Set DNS Alias when the ACME challenge should be created under another domain. Acmebot will use `_acme-challenge.<dnsAlias>` for validation.

This is useful when you delegate validation to a separate zone. Ensure the required CNAME or delegation exists before submitting the request.

### Tags

Custom tags are written to the Key Vault certificate. The `Acmebot` tag is reserved for internal metadata and cannot be set manually.

## Renew a Certificate

Manual renewal reuses the existing certificate policy from Key Vault and starts a new issuance orchestration. Use this when you need to rotate a certificate before the scheduled renewal window.

Scheduled renewals run daily and add a random delay of up to 600 seconds before processing due certificates.

## Revoke a Certificate

Revocation sends the existing certificate to the configured ACME CA's revoke endpoint. It does not delete the Key Vault certificate. After revocation, decide whether to delete, disable, or replace the certificate version according to your operational policy.

## Operation Status

Issue and renew operations return an operation URL. The dashboard polls the operation until it completes or fails.

If an operation fails:

- Read the displayed problem message.
- Check Application Insights for the orchestration instance ID.
- Verify DNS records and provider permissions.
- Retry after fixing the underlying issue.
