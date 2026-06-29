---
description: "Frequently asked questions about Acmebot: automatic renewal, DNS-01 validation, Azure Key Vault storage, and certificate lifecycle."
---

# FAQ

## How does automatic renewal work?

The `RenewCertificates` timer runs daily for enabled managed certificates. When ACME renewal information is available, Acmebot uses the CA's suggested renewal window and `Retry-After` timing to decide the next check, then renews after the suggested window has started. Otherwise, it renews when the remaining certificate lifetime is no more than `Acmebot__RenewBeforeExpiry` percent (default 30):

```text
Acmebot__RenewBeforeExpiry=30
```

Azure Functions timer schedules run in UTC unless the hosting plan supports `WEBSITE_TIME_ZONE`.

## Can I use an existing Key Vault?

Yes. Deploy Acmebot against the existing vault and grant the Function App identity permission to read and manage certificates. For vaults using Azure RBAC, `Key Vault Certificates Officer` is the typical role; vaults using access policies need equivalent certificate permissions.

## Can multiple Azure services use the same certificate?

Yes. Acmebot stores the certificate in Key Vault, and multiple services can consume it from there. Each consuming service still needs its own Key Vault access and TLS binding. See [Azure Service Integration](./service-integration).

## How do I remove a certificate from Acmebot management?

Delete it from Key Vault. If it must also be revoked at the certificate authority, revoke it from the dashboard or HTTP API before deleting it. Revocation also disables the current Key Vault certificate version.

## How do I reinstall or upgrade Acmebot without losing certificates?

Keep the Key Vault and its certificates in place. Acmebot rediscovers managed certificates from Key Vault metadata after redeployment. When replacing the Function App, use a user-assigned managed identity if you need a stable identity across app recreation.

## How do I add or remove DNS names on an existing certificate?

Issue a new certificate with the desired DNS names, or update the Key Vault certificate policy and renew. For production endpoints, plan the downstream service sync separately: a new Key Vault version does not mean the consuming service has already deployed it.

## Why does the dashboard show no DNS zones?

Common causes are missing provider settings, invalid credentials, insufficient zone permissions, or a provider account that cannot list zones through its API. For Azure DNS, confirm the Function App identity has DNS zone access in the subscription that contains the zone. For external providers, confirm the token can list zones and edit TXT records. See [DNS Providers](./dns-providers).

## Can I use wildcard certificates?

Yes. Acmebot uses DNS-01 validation, so wildcard certificates are supported. Request `*.example.com` from the dashboard or API, and make sure the configured DNS provider controls the validation zone.

## Can I use a private DNS zone?

Azure Private DNS is supported as a provider, but public ACME certificate authorities must be able to resolve the DNS-01 challenge. For public certificates, use public DNS validation unless your DNS design intentionally delegates `_acme-challenge` to a public validation zone.

## How should I store DNS provider secrets?

Provider credentials are Function App app settings, so treat them as secrets. When possible, store each value in Key Vault and reference it with an App Service Key Vault reference. Scope provider tokens to the smallest set of zones Acmebot needs. See [Security](../reference/security) for the full guidance.

## How much does Acmebot cost to run?

For low-volume deployments, the Azure cost is usually small: Acmebot runs on serverless Functions with modest storage and telemetry. Exact cost depends on your region, hosting plan, telemetry volume, certificate volume, and Key Vault usage. Review current Azure pricing for Functions, Storage, Application Insights, Log Analytics, and Key Vault before production rollout.

## Where should I start when issuance or renewal fails?

Start with the operation status, then inspect Application Insights to find whether the failure is in authentication, DNS validation, Key Vault access, ACME CA communication, or downstream service sync. See [Troubleshooting](./troubleshooting).
