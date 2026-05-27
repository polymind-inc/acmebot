# Azure Service Integration

Acmebot stores issued certificates in Azure Key Vault. The consuming Azure service is responsible for importing, referencing, or syncing that certificate version into its own TLS configuration.

Treat certificate issuance and service rollout as two connected workflows:

1. Acmebot renews the certificate and creates a new Key Vault certificate version.
2. The Azure service reads that version according to its own Key Vault integration behavior.
3. You verify that the public endpoint is serving the renewed certificate.

## Integration Principles

- Keep the Key Vault certificate name stable so downstream references do not need to change on every renewal.
- Prefer `Latest` or versionless Key Vault references when the consuming service supports automatic rotation.
- Grant the consuming Azure service read access to the certificate or secret in Key Vault.
- Confirm the certificate subject or SANs match the custom domain configured on the service.
- Monitor both Acmebot renewal and the downstream service sync state.
- Test with a staging endpoint or low-risk domain before moving production traffic.

## Service Matrix

| Azure service | Recommended pattern | Rotation behavior |
| --- | --- | --- |
| App Service, Azure Functions, Web App for Containers | Import the Key Vault certificate into App Service certificates, then bind it to a custom domain. | App Service automatically syncs newer Key Vault certificate versions, typically within 24 hours. |
| Azure Container Apps | Import the certificate from Key Vault into the Container Apps environment and bind it to the custom domain. | Review Container Apps certificate limitations before choosing key type and curve. |
| Application Gateway v2 | Reference a Key Vault certificate or secret for HTTPS listeners. | Use a versionless secret identifier so Application Gateway can pick up new versions automatically. |
| Azure Front Door Standard/Premium | Add a Key Vault certificate as a Front Door secret and select `Latest` as the version. | Front Door can automatically deploy the newer version when the Key Vault certificate is renewed. |
| API Management | Configure custom domains with Key Vault-backed certificates. | Keep the APIM identity authorized to read the Key Vault certificate. |
| Azure SignalR Service | Configure a custom domain with a certificate stored in Key Vault. | Verify service-specific certificate sync after renewal. |
| Virtual Machines | Use the Key Vault VM extension or your own provisioning workflow to install the certificate. | Your provisioning workflow controls rollout and reload timing. |

## App Service

Use App Service certificate import when the target is Azure App Service, Azure Functions, or Web App for Containers.

1. Open the App Service resource.
2. Go to **Certificates**.
3. Add a bring-your-own certificate from Key Vault.
4. Bind the imported certificate to the custom domain.
5. Keep the Key Vault certificate and App Service resource provider permissions in place.

Reference: [Import a certificate from Key Vault - Azure App Service](https://learn.microsoft.com/azure/app-service/configure-ssl-certificate#import-a-certificate-from-key-vault)

If Key Vault shows a renewed certificate but App Service is still serving the old certificate, check the imported certificate status in App Service and confirm the App Service resource provider can still read the vault.

## Azure Container Apps

Container Apps can import a Key Vault certificate into the Container Apps environment. Use this when custom domains are served directly by Container Apps.

Before selecting the key type, review the current Container Apps certificate limitations. If your organization standardizes on ECDSA certificates, validate that the selected curve is supported by Container Apps.

Reference: [Import certificates from Azure Key Vault to Azure Container Apps](https://learn.microsoft.com/azure/container-apps/key-vault-certificates-manage)

## Application Gateway v2

Application Gateway v2 supports TLS termination with Key Vault certificates. For automatic rotation, configure the listener with a Key Vault secret identifier that does not include a specific version.

Recommended checks:

- Application Gateway uses the v2 SKU.
- The certificate private key is exportable when required by the service.
- The Application Gateway identity can read the Key Vault certificate or secret.
- The Key Vault URI is versionless so newer versions can be used automatically.

Reference: [TLS termination with Key Vault certificates](https://learn.microsoft.com/azure/application-gateway/key-vault-certs)

## Azure Front Door Standard/Premium

For customer-managed certificates, create a Front Door secret from the Key Vault certificate and select `Latest` as the certificate version. This avoids updating the Front Door configuration every time Acmebot renews the certificate.

Recommended checks:

- The Key Vault is accessible to Front Door.
- The certificate is selected as `Latest`, not pinned to a specific version.
- The custom domain CN or SAN matches the certificate.
- You allow time for Front Door to deploy the renewed version globally.

Reference: [Configure HTTPS on an Azure Front Door custom domain](https://learn.microsoft.com/azure/frontdoor/standard-premium/how-to-configure-https-custom-domain#use-your-own-certificate)

## API Management

API Management custom domains can use certificates stored in Key Vault. This is useful when Acmebot is responsible for renewal and APIM owns the public gateway endpoint.

Recommended checks:

- APIM has a managed identity enabled.
- The identity can read the Key Vault certificate.
- The custom domain is configured to use the Key Vault certificate.
- Gateway endpoints are verified after renewal.

Reference: [Configure a custom domain name for Azure API Management](https://learn.microsoft.com/azure/api-management/configure-custom-domain)

## SignalR Service

Azure SignalR Service supports custom domains with certificates. Store the certificate in Key Vault and configure the SignalR custom domain to use it.

Reference: [Configure a custom domain for Azure SignalR Service](https://learn.microsoft.com/azure/azure-signalr/howto-custom-domain)

## Virtual Machines and Other Workloads

For VM-based workloads, use the Key Vault VM extension or an existing configuration management pipeline to retrieve the certificate, install it, and reload the application.

References:

- [Azure Key Vault VM Extension for Windows](https://learn.microsoft.com/azure/virtual-machines/extensions/key-vault-windows)
- [Azure Key Vault VM Extension for Linux](https://learn.microsoft.com/azure/virtual-machines/extensions/key-vault-linux)
- [Export certificates from Azure Key Vault](https://learn.microsoft.com/azure/key-vault/certificates/how-to-export-certificate)

## Operational Checklist

After Acmebot renews a certificate:

- Confirm the Key Vault certificate has a new current version.
- Confirm the consuming service can still access Key Vault.
- Confirm the consuming service is configured for latest or versionless rotation when available.
- Check the endpoint from outside Azure and verify the served certificate expiry date.
- Keep an emergency manual sync or redeploy procedure for services that do not rotate immediately.

If Key Vault is current but the public endpoint is not, the remaining issue is usually the consuming service configuration rather than ACME issuance.
