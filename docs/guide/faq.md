# FAQ

## How does automatic renewal work?

The `RenewCertificates` timer runs daily. Acmebot renews a managed certificate when the ACME CA reports that the renewal window has opened, or when the certificate expires within `Acmebot__RenewBeforeExpiry` days.

The default renewal window is 30 days:

```text
Acmebot__RenewBeforeExpiry=30
```

Azure Functions timer schedules run in UTC unless the hosting plan supports `WEBSITE_TIME_ZONE`.

## Can I use an existing Key Vault?

Yes. Deploy Acmebot against the existing vault and grant the Function App identity permission to read and manage certificates.

For new deployments that use Azure RBAC, `Key Vault Certificates Officer` is the typical role for the Acmebot identity. Existing vaults that use access policies need equivalent certificate permissions.

## Can multiple Azure services use the same certificate?

Yes. Acmebot stores the certificate in Key Vault, and multiple services can consume it from there. Each consuming service still needs its own Key Vault access and TLS binding configuration.

See [Azure Service Integration](./service-integration).

## How do I remove a certificate from Acmebot management?

Delete the certificate from Key Vault if Acmebot should stop managing it.

If the certificate must also be revoked at the certificate authority, revoke it from the dashboard or HTTP API before deleting it from Key Vault.

## How do I reinstall or upgrade Acmebot without losing certificates?

Keep the Key Vault and its certificates in place. Acmebot discovers managed certificates from Key Vault metadata after the app is redeployed.

When replacing the Function App, use a user-assigned managed identity if you need a stable identity across app recreation.

## How do I add or remove DNS names from an existing certificate?

Issue a new certificate with the desired DNS names, or update the Key Vault certificate policy and renew the certificate.

For production endpoints, plan the downstream service sync separately. A new Key Vault certificate version does not always mean the consuming service has already deployed it.

## Why does the dashboard show no DNS zones?

Common causes are missing provider settings, invalid credentials, insufficient zone permissions, or a provider account that cannot list zones through its API.

For Azure DNS, verify the Function App identity has DNS zone access in the subscription that contains the zone. For external providers, verify the token can list zones and edit TXT records.

See [DNS Providers](./dns-providers).

## Can I use wildcard certificates?

Yes. Acmebot uses DNS-01 validation, so wildcard certificates are supported. Request `*.example.com` from the dashboard or API and make sure the configured DNS provider controls the validation zone.

## Can I use a private DNS zone?

Azure Private DNS is supported as a provider, but public ACME certificate authorities must be able to validate the DNS-01 challenge through resolvable DNS. For public certificates, use public DNS validation unless your DNS design intentionally delegates `_acme-challenge` to a public validation zone.

## How should I store DNS provider secrets?

Provider credentials are Function App app settings. Treat them as secrets.

When possible, store the actual secret in Key Vault and use an App Service Key Vault reference in the Function App setting. Scope provider tokens to the smallest set of zones Acmebot needs.

Reference: [Use Key Vault references in App Service and Azure Functions](https://learn.microsoft.com/azure/app-service/app-service-key-vault-references)

## How much does Acmebot cost to run?

For low-volume deployments, the Azure platform cost is usually small because Acmebot runs on serverless Functions and uses modest storage and telemetry. Exact cost depends on your Azure region, hosting plan, telemetry volume, certificate volume, and Key Vault usage.

Review current Azure pricing for Azure Functions, Storage, Application Insights, Log Analytics, and Key Vault before production rollout.

## Where should I start when issuance or renewal fails?

Start with the operation status, then inspect Application Insights to determine whether the failure is in authentication, DNS validation, Key Vault access, ACME CA communication, or downstream Azure service sync.

See [Troubleshooting](./troubleshooting).
