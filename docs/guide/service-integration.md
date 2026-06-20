# Azure Service Integration

Acmebot stores issued certificates in Azure Key Vault. Each consuming Azure service is responsible for importing, referencing, or syncing the current certificate version into its own TLS configuration.

Treat issuance and rollout as two connected workflows:

1. Acmebot renews the certificate and creates a new Key Vault certificate version.
2. The Azure service picks up that version according to its own Key Vault integration.
3. You confirm the public TLS endpoint is serving the renewed certificate.

## Integration Principles

- Keep the Key Vault certificate name stable so downstream references survive each renewal.
- Prefer `Latest` or versionless Key Vault references where the service supports automatic rotation.
- Grant the consuming service read access to the certificate or secret in Key Vault.
- Confirm the certificate subject or SANs match the service's custom domain.
- Monitor both Acmebot renewal and the downstream service's sync state.
- Test with a staging endpoint or low-risk domain before moving production traffic.

## Service Matrix

| Azure service | Recommended pattern | Rotation behavior |
| --- | --- | --- |
| App Service, Azure Functions, Web App for Containers | Import the Key Vault certificate into App Service certificates, then bind it to a custom domain. | App Service syncs newer Key Vault versions automatically, typically within 24 hours. |
| Azure Container Apps | Import the certificate from Key Vault into the Container Apps environment and bind it to the custom domain. | Review Container Apps certificate limitations before choosing key type and curve. |
| Application Gateway v2 | Reference a Key Vault certificate or secret for HTTPS listeners. | Use a versionless secret identifier so new versions are picked up automatically. |
| Azure Front Door Standard/Premium | Add the Key Vault certificate as a Front Door secret and select `Latest`. | Front Door deploys the newer version automatically when the certificate is renewed. |
| API Management | Configure custom domains with Key Vault-backed certificates. | Keep the API Management identity authorized to read the Key Vault certificate. |
| Azure Web PubSub | Create a custom certificate from Key Vault, then bind it to the custom domain. | Leave the Key Vault secret version empty so Web PubSub can apply newer versions automatically. |
| Azure Event Grid Namespaces | Assign custom domains to the namespace HTTP and MQTT hostnames with a Key Vault certificate and managed identity. | Use an unversioned Key Vault certificate URL and complete TXT ownership validation. |
| Azure SignalR Service | Configure a custom domain with a certificate stored in Key Vault. | Verify service-specific certificate sync after renewal. |
| Virtual Machines | Use the Key Vault VM extension or your own provisioning workflow to install the certificate. | Your workflow controls rollout and reload timing. |

## App Service

Use App Service certificate import when the target is Azure App Service, Azure Functions, or Web App for Containers.

1. Open the App Service resource.
2. Go to **Certificates**.
3. Add a bring-your-own certificate from Key Vault.
4. Bind the imported certificate to the custom domain.
5. Keep the Key Vault certificate and App Service resource provider permissions in place.

Reference: [Import a certificate from Key Vault - Azure App Service](https://learn.microsoft.com/azure/app-service/configure-ssl-certificate#import-a-certificate-from-key-vault)

If Key Vault shows a renewed certificate but App Service still serves the old one, check the imported certificate status in App Service and confirm the App Service resource provider can still read the vault.

## Azure Container Apps

Container Apps can import a Key Vault certificate into the environment for custom domains served directly by Container Apps. Before selecting the key type, review the current Container Apps certificate limitations; if you standardize on ECDSA certificates, confirm the curve is supported.

Reference: [Import certificates from Azure Key Vault to Azure Container Apps](https://learn.microsoft.com/azure/container-apps/key-vault-certificates-manage)

## Application Gateway v2

Application Gateway v2 supports TLS termination with Key Vault certificates. For automatic rotation, configure the listener with a Key Vault secret identifier that omits the version.

Recommended checks:

- Application Gateway uses the v2 SKU.
- The certificate private key is exportable when the service requires it.
- The Application Gateway identity can read the Key Vault certificate or secret.
- The Key Vault URI is versionless so newer versions are used automatically.

Reference: [TLS termination with Key Vault certificates](https://learn.microsoft.com/azure/application-gateway/key-vault-certs)

## Azure Front Door Standard/Premium

For customer-managed certificates, create a Front Door secret from the Key Vault certificate and select `Latest`. This avoids reconfiguring Front Door on every renewal.

Recommended checks:

- The Key Vault is accessible to Front Door.
- The certificate is selected as `Latest`, not pinned to a specific version.
- The custom domain CN or SAN matches the certificate.
- You allow time for Front Door to deploy the renewed version globally.

Reference: [Configure HTTPS on an Azure Front Door custom domain](https://learn.microsoft.com/azure/frontdoor/standard-premium/how-to-configure-https-custom-domain#use-your-own-certificate)

## API Management

API Management custom domains can use certificates stored in Key Vault. This pattern fits environments where Acmebot owns renewal and API Management owns the public gateway endpoint.

Recommended checks:

- API Management has a managed identity enabled.
- The identity can read the Key Vault certificate.
- The custom domain is configured to use the Key Vault certificate.
- Gateway endpoints are verified after renewal.

Reference: [Configure a custom domain name for Azure API Management](https://learn.microsoft.com/azure/api-management/configure-custom-domain)

## Azure Web PubSub

Azure Web PubSub supports custom domains on the Premium tier with certificates stored in Key Vault. Create a custom certificate on the Web PubSub resource, grant its managed identity access to Key Vault, and bind that certificate to the custom domain.

Recommended checks:

- The Web PubSub resource uses at least the Premium tier.
- The Web PubSub managed identity can read the Key Vault certificate or secret.
- The custom certificate is not pinned to a Key Vault secret version when automatic rotation is desired.
- The custom domain CNAME points to the default Web PubSub domain.
- The Web PubSub health API succeeds without TLS errors after the binding is active.

Reference: [Add a custom domain to Azure Web PubSub](https://learn.microsoft.com/azure/azure-web-pubsub/howto-custom-domain)

## Azure Event Grid Namespaces

Azure Event Grid Namespaces can assign custom domains to HTTP and MQTT hostnames. Use a Key Vault certificate, enable a namespace managed identity, grant that identity access to the certificate, and complete the TXT record validation generated by Event Grid.

Recommended checks:

- The certificate subject alternative names cover the HTTP or MQTT custom domain.
- The namespace managed identity has access to the Key Vault certificate.
- The custom domain uses the base, unversioned Key Vault certificate identifier.
- The generated TXT record is present before validating the custom domain.
- The namespace endpoint is checked after renewal because Event Grid owns the serving configuration.

References:

- [Assign custom domain names to Event Grid namespace host names](https://learn.microsoft.com/azure/event-grid/assign-custom-domain-name)
- [Microsoft.EventGrid namespaces resource reference](https://learn.microsoft.com/azure/templates/microsoft.eventgrid/namespaces)

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
- Confirm the consuming service is configured for latest or versionless rotation where available.
- Check the endpoint from outside Azure and verify the served certificate's expiry date.
- Keep an emergency manual sync or redeploy procedure for services that do not rotate immediately.

If Key Vault is current but the public TLS endpoint is not, the remaining issue is usually the consuming service's configuration rather than ACME issuance.
