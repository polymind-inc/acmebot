# Getting Started

This walkthrough deploys a first Acmebot v5 environment and issues a certificate into Azure Key Vault.

## Prerequisites

- An Azure Public subscription where you can create Function App, Storage, monitoring, and Key Vault resources.
- Permission to create role assignments, which the template needs when it configures Key Vault access.
- A DNS zone hosted by a supported provider.
- Credentials or managed identity access that can create and delete TXT records in that zone.
- A contact email address for the ACME account.
- A Microsoft Entra ID or App Service Authentication configuration for dashboard access.

## 1. Choose an ACME Endpoint

For a first test, use a staging endpoint if your certificate authority offers one. For production, the deployment form includes these verified endpoints:

| CA | Endpoint |
| --- | --- |
| Let's Encrypt | `https://acme-v02.api.letsencrypt.org/directory` |
| GlobalSign | `https://emea.acme.atlas.globalsign.com/directory` |
| Google Trust Services | `https://dv.acme-v02.api.pki.goog/directory` |
| SSL.com ECC | `https://acme.ssl.com/sslcom-dv-ecc` |
| SSL.com RSA | `https://acme.ssl.com/sslcom-dv-rsa` |
| ZeroSSL | `https://acme.zerossl.com/v2/DV90` |

If a verified CA requires external account binding, the deployment form fixes the credential type to EAB. For custom endpoints, select EAB when your CA requires it. See [Certificate Authorities](./certificate-authorities) for details.

## 2. Deploy Acmebot

Open [Deployment](./deployment) and deploy to Azure Public. During deployment:

1. Select the subscription, resource group, and region.
2. Choose a resource naming mode.
3. Enter the ACME endpoint and contact email.
4. Configure one DNS provider.
5. Choose a system-assigned or user-assigned managed identity.
6. Create a new Key Vault or select an existing one.
7. Create a new Log Analytics workspace or select an existing one.

The template creates the Function App, Flex Consumption plan, Storage account, Application Insights component, and required app settings.

## 3. Grant DNS Access

The template configures Key Vault access for the Function App identity, but DNS access depends on the provider.

- **Azure DNS**: assign the Function App identity a role that can read zones and manage TXT records, such as `DNS Zone Contributor`, on the zone or a resource group that contains only the relevant zones.
- **Azure Private DNS**: assign `Private DNS Zone Contributor` on the private zone or resource group.
- **External providers**: use credentials scoped to the hosted zones Acmebot should manage, and prefer least-privilege API tokens when the provider supports them.

When possible, store provider secrets in Key Vault and reference them from Function App settings with App Service Key Vault references. App configuration stays readable while the secret value moves under Key Vault access control.

## 4. Enable Dashboard Authentication

The dashboard and HTTP API require authenticated requests. Configure App Service Authentication on the Function App and require sign-in before requests reach the app. A typical setup uses Microsoft Entra ID as the identity provider.

After authentication is enabled, browse to the Function App URL and sign in. To require app roles for issue and revoke operations, see [Security](../reference/security).

## 5. Issue Your First Certificate

In the dashboard:

1. Open the certificate creation dialog.
2. Select the DNS provider and zone.
3. Enter the record name. Leave it empty for the zone apex, or use `*` for a wildcard certificate.
4. Review the full DNS name that will be requested.
5. Keep the default RSA 2048 key unless you need a different key type.
6. Submit the request.

Acmebot creates the `_acme-challenge` TXT record, waits for propagation, finalizes the ACME order, and stores the certificate in Key Vault.

## 6. Verify the Result

After the operation completes:

- Confirm the certificate appears in the dashboard.
- Open the Key Vault and confirm the certificate has a current version.
- Check Application Insights if the operation failed or stayed pending.
- Confirm that downstream Azure services can read the certificate from Key Vault or import the PFX.

## 7. Let Renewals Run

The `RenewCertificates` timer runs daily. A certificate is renewed when either:

- The ACME server reports that the suggested renewal window has started, or
- The certificate expires within `Acmebot__RenewBeforeExpiry` days (default 30).

See [Operations](./operations) for monitoring and troubleshooting guidance.

## Next Steps

- Configure more providers in [DNS Providers](./dns-providers).
- Review CA-specific notes in [Certificate Authorities](./certificate-authorities).
- Learn dashboard operations in [Dashboard](./dashboard).
- Connect certificates to Azure services in [Azure Service Integration](./service-integration).
- Keep [Troubleshooting](./troubleshooting) nearby for first-issuance validation.
- Review every app setting in [Configuration](../reference/configuration).
