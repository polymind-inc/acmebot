---
description: "Acmebot HTTP API reference: the same-origin endpoints behind the dashboard for issuing, renewing, and managing certificates."
---

# HTTP API

The dashboard uses these same-origin HTTP endpoints. They define the integration surface and operation lifecycle.

All endpoints expect authenticated requests. Issue, renew, and revoke operations may also require app roles when `Acmebot__RequireAppRoles=true`.

## Authentication

The v5 API is intended to be protected by App Service Authentication. For interactive use, users sign in through the dashboard. For automation, call the API with an authenticated principal that App Service Authentication can validate, typically a Microsoft Entra ID bearer token.

```http
Authorization: Bearer <access-token>
Accept: application/json
```

The HTTP triggers use anonymous trigger authorization internally, but the application code rejects requests without an authenticated user. A Functions host key by itself does not satisfy the dashboard or API authentication checks.

When app role enforcement is enabled, issue and renew operations require `Acmebot.IssueCertificate`, and revoke operations require `Acmebot.RevokeCertificate`.

## Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/account` | Show the directory URL, CAA identities, and DNS-PERSIST-01 discovery metadata. |
| `GET` | `/api/configuration` | Show current ACME, general, and DNS provider settings without credential values. |
| `GET` | `/api/certificates` | List certificates from Key Vault. |
| `POST` | `/api/certificates` | Start certificate issuance. |
| `POST` | `/api/certificates/{certificateName}/renew` | Start manual renewal. |
| `POST` | `/api/certificates/{certificateName}/revoke` | Revoke a certificate through the ACME certificate authority. |
| `GET` | `/api/dns-zones` | List DNS zones from configured providers. |
| `GET` | `/api/renewals` | List automatic renewal status for certificates. |
| `GET` | `/api/operations/{instanceId}` | Poll an issuance or renewal operation. |

## Show Current Configuration

`GET /api/configuration` requires an authenticated user and no app role, including when `Acmebot__RequireAppRoles=true`. Unauthenticated requests return `401 Unauthorized`. Responses use `Cache-Control: no-store`.

The response contains `acme` and `general` sections and a `dnsProviders` array. Each section has a `name` and `settings` array. Each setting has `name`, `value`, and `isConfigured`: ordinary settings expose their value (or `null` when unset) with `isConfigured: null`; credentials expose `value: null` and only a boolean `isConfigured`. A configured credential means a nonblank value was loaded, not that the credential is valid or connectivity has been checked.

Only explicitly selected fields are returned. Credential values, including API keys, tokens, EAB identifiers and HMAC keys, service account keys, and private key references, are omitted. URL user information, query strings, and fragments are removed. Webhook URLs and arbitrary environment variables are not included.

This endpoint reports options loaded by the application; it does not contact providers, register an ACME account, or change settings. It does not validate whether the current settings can issue certificates.

## Show ACME Account

```http
GET /api/account
Accept: application/json
```

```json
{
  "directoryUrl": "https://acme-v02.api.letsencrypt.org/directory",
  "caaIdentities": ["letsencrypt.org"],
  "dnsPersist01": {
    "preProvisioningMetadataAvailable": false,
    "issuerDomainNames": [],
    "accountHashPrefix": null
  }
}
```

This endpoint requires authentication and no app role, including when `Acmebot__RequireAppRoles=true`. Unauthenticated requests return `401 Unauthorized`. The response contains no account URL or private key material. Acmebot retains the account URL internally for ACME requests and future persistent-record generation; users do not need to copy it. Clients consuming the former `accountUri` response field must stop relying on it.

`directoryUrl` is the configured `Acmebot__Endpoint`. `caaIdentities` comes from the ACME directory metadata and is an empty array when the CA does not advertise it. These identities do not establish DNS-PERSIST-01 support.

`dnsPersist01` reports the directory's `issuerDomainNames` and `accountHashPrefix` separately from CAA identities. Missing values are represented by `[]` and `null`. `preProvisioningMetadataAvailable` is true only when the issuer names and hash prefix pass draft `-02` validation; false means the required inputs are missing or malformed, not that every interactive challenge is unsupported. Parsed metadata values are returned as advertised, even when they fail validation. No account hash or TXT record is generated, and this flag does not indicate that Acmebot supports DNS-PERSIST-01 issuance.

The first successful call registers an ACME account and saves its key and account state when no account exists yet, even if this deployment has never issued a certificate. Configure the contact email and any required external account binding credentials before calling it. No DNS records are changed and no certificate order is created.

The dashboard's **Account** panel uses this endpoint. See the [DNS-PERSIST-01 operating model](/guide/dns-persist-01) for current limitations.

## Operation Lifecycle

`POST /api/certificates` and `POST /api/certificates/{certificateName}/renew` return `202 Accepted` with a `Location` header. The header holds a path relative to the request, so resolve it against the endpoint you called. Poll that URL until it returns:

| Status | Meaning |
| --- | --- |
| `202` | Operation is pending or running. |
| `200` | Operation completed. |
| Problem response | Operation failed. |

## Issue Certificate

```http
POST /api/certificates
Content-Type: application/json
Accept: application/json
```

```json
{
  "certificateName": "wildcard-example-com",
  "dnsNames": ["*.example.com"],
  "dnsProviderName": "Azure DNS",
  "keyType": "RSA",
  "keySize": 2048,
  "reuseKey": false,
  "dnsAlias": "acme-validation.example.net",
  "profile": "tlsserver",
  "tags": {
    "owner": "platform"
  }
}
```

### Request Fields

| Property | Required | Description |
| --- | --- | --- |
| `certificateName` | Yes | Key Vault certificate name. API clients must provide this value. It must be 1 to 127 characters and contain only letters, numbers, and hyphens. |
| `dnsNames` | Yes | ASCII/punycode DNS names to include in the certificate. Omit trailing dots. Wildcards are allowed only as the leftmost label. |
| `dnsProviderName` | Yes | Provider display name, such as `Azure DNS` or `Cloudflare`. When `dnsAlias` is set, this provider must manage the DNS alias zone. |
| `keyType` | Yes | `RSA`, `RSA-HSM`, `EC`, or `EC-HSM`. The `-HSM` variants require a Premium-tier Key Vault and are only available through the API and CLI — the dashboard's Add Certificate dialog does not offer them. Certificates issued with an HSM key type still display correctly in the dashboard's list and details views. |
| `keySize` | For RSA/RSA-HSM | `2048`, `3072`, or `4096`. |
| `keyCurveName` | For EC/EC-HSM | `P-256`, `P-384`, `P-521`, or `P-256K`. |
| `reuseKey` | No | Whether Key Vault should reuse the certificate key. |
| `dnsAlias` | No | Alternate ASCII/punycode domain used for DNS-01 validation. Acmebot writes TXT records at `_acme-challenge.<dnsAlias>`, so omit the `_acme-challenge` prefix and trailing dot from this value. |
| `profile` | No | ACME profile to request for this certificate. When omitted, Acmebot uses `Acmebot__PreferredProfile` if configured. |
| `tags` | No | Custom Key Vault certificate tags. `Acmebot` is reserved. |

For delegated DNS-01 validation, set `dnsNames` to the certificate names and set `dnsAlias` to a unique record in a zone Acmebot can update. For each DNS name, create a CNAME from `_acme-challenge.<dnsName>` to `_acme-challenge.<dnsAlias>` in the authoritative DNS provider for the certificate domain.

### Replacing a Certificate Not Managed by Acmebot

If `certificateName` matches a Key Vault certificate that Acmebot does not manage, such as one imported from another certificate authority, the request returns `409 Conflict` and starts nothing. To replace that certificate with an ACME certificate, send the request again with the `replaceExisting` query parameter:

```http
POST /api/certificates?replaceExisting=true
Content-Type: application/json
Accept: application/json
```

The issued certificate is added as a new version of the existing Key Vault certificate, so services that reference the certificate without a version start using it. Certificates that Acmebot already manages are reissued without this parameter.

Ownership is checked again right before the certificate is written to Key Vault. If a certificate that Acmebot does not manage appears under the same name while issuance or renewal is in progress, the operation fails without replacing it unless `replaceExisting=true` was set.

## List Certificates

```http
GET /api/certificates
Accept: application/json
```

Returns an array of certificate objects.

```json
[
  {
    "id": "https://my-vault.vault.azure.net/certificates/wildcard-example-com/...",
    "name": "wildcard-example-com",
    "dnsNames": ["*.example.com"],
    "dnsProviderName": "Azure DNS",
    "createdOn": "2026-05-01T00:00:00+00:00",
    "expiresOn": "2026-07-30T00:00:00+00:00",
    "x509Thumbprint": "ABCDEF...",
    "keyType": "RSA",
    "keySize": 2048,
    "reuseKey": false,
    "enabled": true,
    "isIssuedByAcmebot": true,
    "isSameEndpoint": true,
    "acmeEndpoint": "acme-v02.api.letsencrypt.org",
    "dnsAlias": "",
    "tags": {
      "owner": "platform"
    }
  }
]
```

## List DNS Zones

```http
GET /api/dns-zones
Accept: application/json
```

```json
[
  {
    "dnsProviderName": "Azure DNS",
    "dnsZones": [
      { "name": "example.com" }
    ]
  }
]
```

## List Renewal Status

```http
GET /api/renewals
Accept: application/json
```

Returns automatic renewal status for certificates visible to Acmebot.

```json
[
  {
    "certificateName": "wildcard-example-com",
    "status": "Scheduled",
    "statusKind": "scheduled",
    "message": "Renewal is scheduled within the certificate authority's suggested renewal window.",
    "nextCheck": "2026-06-20T00:00:00+00:00",
    "lastCheckedAt": "2026-06-19T00:00:00+00:00"
  }
]
```

## Manual Renewal

```http
POST /api/certificates/wildcard-example-com/renew
Accept: application/json
```

Returns `202 Accepted` with a `Location` header for operation polling. Renewal restores the per-certificate ACME profile saved in Acmebot metadata; certificates without one use the deployment-level `Acmebot__PreferredProfile` setting when configured.

Only certificates with Acmebot metadata can be renewed, including those issued through a different ACME endpoint. A missing certificate returns `404 Not Found`, and a certificate not managed by Acmebot returns `409 Conflict`; to replace it, see [Replacing a Certificate Not Managed by Acmebot](#replacing-a-certificate-not-managed-by-acmebot).

## Revocation

```http
POST /api/certificates/wildcard-example-com/revoke
Accept: application/json
```

Revocation waits for the ACME revoke operation to complete, disables the current Key Vault certificate version, and returns `200 OK` on success.

Only certificates that Acmebot issued through the currently configured ACME endpoint can be revoked. A missing certificate returns `404 Not Found`, and any other certificate returns `409 Conflict`; revoke those through the account or certificate authority that issued them.

## Errors

Validation errors return a problem response that may include field-specific errors. Orchestration failures return problem details from the failed Durable Functions instance.

Common statuses:

| Status | Meaning |
| --- | --- |
| `401` | Request is not authenticated. |
| `403` | User does not have the required app role. |
| `400` | Request validation failed or operation instance was not found. |
| `500` | Operation failed unexpectedly. |
