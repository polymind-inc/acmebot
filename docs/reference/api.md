# HTTP API

The dashboard uses these same-origin HTTP endpoints. They are useful for understanding the integration surface and operation lifecycle.

All endpoints expect authenticated requests. Issue and revoke operations may also require app roles when `Acmebot:AppRoleRequired=true`.

## Authentication

The v5 API is intended to be protected by App Service Authentication. For interactive use, users sign in through the dashboard. For automation, call the API with an authenticated principal that App Service Authentication can validate, typically a Microsoft Entra ID bearer token.

```http
Authorization: Bearer <access-token>
Accept: application/json
```

The Function triggers use anonymous trigger authorization internally, but the application code rejects requests without an authenticated user. A Functions host key by itself does not satisfy the dashboard or API authentication checks.

When app role enforcement is enabled, issue and renew operations require `Acmebot.IssueCertificate`, and revoke operations require `Acmebot.RevokeCertificate`.

## Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/certificates` | List certificates from Key Vault. |
| `POST` | `/api/certificates` | Start certificate issuance. |
| `POST` | `/api/certificates/{certificateName}/renew` | Start manual renewal. |
| `POST` | `/api/certificates/{certificateName}/revoke` | Revoke a certificate through the ACME CA. |
| `GET` | `/api/dns-zones` | List DNS zones from configured providers. |
| `GET` | `/api/operations/{instanceId}` | Poll an issuance or renewal operation. |

## Operation Lifecycle

`POST /api/certificates` and `POST /api/certificates/{certificateName}/renew` return `202 Accepted` with a `Location` header. Poll that URL until it returns:

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
  "tags": {
    "owner": "platform"
  }
}
```

### CertificatePolicyItem

| Property | Required | Description |
| --- | --- | --- |
| `certificateName` | No | Key Vault certificate name. If omitted, Acmebot derives it from the first DNS name. |
| `dnsNames` | Yes | DNS names to include in the certificate. |
| `dnsProviderName` | No | Provider display name, such as `Azure DNS` or `Cloudflare`. Required when Acmebot cannot infer a single provider. |
| `keyType` | Yes | `RSA` or `EC`. |
| `keySize` | For RSA | `2048`, `3072`, or `4096`. |
| `keyCurveName` | For EC | `P-256`, `P-384`, `P-521`, or `P-256K`. |
| `reuseKey` | No | Whether Key Vault should reuse the certificate key. |
| `dnsAlias` | No | Alternate domain used for DNS-01 validation. |
| `tags` | No | Custom Key Vault certificate tags. `Acmebot` is reserved. |

## List Certificates

```http
GET /api/certificates
Accept: application/json
```

Returns an array of `CertificateItem`.

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
    "isExpired": false,
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

## Manual Renewal

```http
POST /api/certificates/wildcard-example-com/renew
Accept: application/json
```

Returns `202 Accepted` with a `Location` header for operation polling.

## Revocation

```http
POST /api/certificates/wildcard-example-com/revoke
Accept: application/json
```

Revocation waits for the ACME revoke operation to complete and returns `200 OK` on success.

## Errors

Validation errors return a problem response that may include field-specific errors. Orchestration failures return problem details from the failed Durable Functions instance.

Common statuses:

| Status | Meaning |
| --- | --- |
| `401` | Request is not authenticated. |
| `403` | User does not have the required app role. |
| `400` | Request validation failed or operation instance was not found. |
| `500` | Operation failed unexpectedly. |
