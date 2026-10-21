---
title: Application Gateway TLS Certificates
description: Connect Acmebot certificates in Key Vault to Application Gateway v2 HTTPS listeners, configure versionless rotation, and verify the public TLS endpoint.
---

# Application Gateway TLS Certificates

Use Acmebot to issue and renew a certificate in Key Vault, then let Application Gateway v2 retrieve it for an HTTPS listener. Acmebot does not create listeners or change routing rules. See the [integration overview](../service-integration) for other consuming services.

## When to Use This Integration

This fits applications that already need Application Gateway for regional routing or a web application firewall and need ACME renewal, a particular CA, wildcard names, or a shared Key Vault certificate.

Application Gateway's Key Vault integration consumes certificates; it does not issue them. If your application can be served directly by App Service or Front Door and its native managed certificate meets your domain and CA requirements, that can remove the need to operate a separate issuer. Evaluate the service's current managed certificate limitations before choosing Acmebot.

## Prerequisites

- An Application Gateway **Standard_v2** or **WAF_v2** resource with a public frontend, a reachable backend, and an appropriate routing rule. Key Vault integration is limited to v2.
- A working [Acmebot deployment](../getting-started) with DNS-01 permissions for the certificate's names and write access to its configured Key Vault.
- An enabled Key Vault certificate with an **exportable, software-backed private key** and a PFX backing secret. HSM-backed keys cannot be used for this integration. Start with Acmebot's RSA 2048 default unless you have verified other requirements for your environment.
- A certificate SAN covering the public hostname. A wildcard such as `*.example.com` does not cover `example.com` or `a.b.example.com`. Use a production CA chain trusted by your clients.
- A **user-assigned managed identity** for Application Gateway. Its read access is separate from the Acmebot Function App's certificate issuance permissions. Only one managed identity can be attached to a gateway; check existing bindings before changing it.
- Permission to configure the gateway and assign the consuming identity access to Key Vault. The operator also needs access to certificate metadata for the PowerShell procedure below.
- DNS control for both ACME validation and routing the application hostname to the gateway. Creating `_acme-challenge` TXT records does not configure application routing.

The [Microsoft Key Vault integration guide](https://learn.microsoft.com/azure/application-gateway/key-vault-certs) documents these service constraints and the current Portal and PowerShell procedures.

## 1. Issue and Inspect the Certificate

Complete [Getting Started](../getting-started) through Key Vault verification. In Key Vault, inspect the current certificate's names, expiry, policy, and enabled state. Keep its certificate name stable across renewals.

Application Gateway needs the certificate's **backing secret URI**, with a `/secrets/` path. A `/certificates/` identifier refers to the public certificate object and is not the listener's secret reference. Do not download or paste the PFX or private key into deployment files.

## 2. Grant the Gateway Identity Read Access

Create or select a user-assigned identity and grant it access using the vault's existing permission model:

- **Azure RBAC:** assign **Key Vault Secrets User** on the certificate vault. Use a vault dedicated to the application/environment, rather than a subscription-wide role. Where a shared vault requires access to just one certificate, evaluate a role assignment on its backing secret instead; account for the operator's separate discovery permissions. See [Key Vault RBAC scope guidance](https://learn.microsoft.com/azure/key-vault/general/rbac-guide#best-practices-for-individual-keys-secrets-and-certificates-role-assignments).
- **Vault access policy:** grant the gateway identity **Get** under **Secret permissions**. Save the policy.

The gateway needs read access, not certificate creation or vault administration rights. Keep Acmebot's existing write permissions in place and allow time for role assignment propagation.

Configure network access as well as identity access. For a restricted vault using service endpoints, follow Microsoft's procedure to enable the `Microsoft.KeyVault` service endpoint on the gateway subnet, allow that subnet in the vault, and allow trusted services through the vault firewall. With a Key Vault private endpoint, ensure private connectivity and link the `privatelink.vaultcore.azure.net` private DNS zone to the gateway's virtual network, including when custom DNS servers are used. See [Key Vault firewall requirements](https://learn.microsoft.com/azure/application-gateway/key-vault-certs#verify-firewall-permissions-to-key-vault).

## 3. Add a Versionless Key Vault Reference

For a vault using **Azure RBAC**, the Portal cannot initially select the Key Vault certificate for this integration. Use Azure PowerShell, CLI, Bicep, or ARM for the reference, then select it on the listener in the Portal. The following Azure PowerShell example configures an existing gateway with a new certificate reference. Run it after signing in with `Connect-AzAccount` and selecting the intended subscription with `Set-AzContext`. Install the Az.Network, Az.ManagedServiceIdentity, and Az.KeyVault modules if needed.

Enter your actual resource names when prompted. Use a new, unused gateway certificate reference name; review the selected identity before applying to a gateway with existing listeners.

```powershell
$gatewayGroup = Read-Host 'Application Gateway resource group'
$gatewayName = Read-Host 'Application Gateway name'
$identityGroup = Read-Host 'User-assigned identity resource group'
$identityName = Read-Host 'User-assigned identity name'
$vaultName = Read-Host 'Key Vault name'
$certificateName = Read-Host 'Key Vault certificate name'
$referenceName = Read-Host 'New gateway certificate reference name'

$gateway = Get-AzApplicationGateway -ResourceGroupName $gatewayGroup -Name $gatewayName
$identity = Get-AzUserAssignedIdentity -ResourceGroupName $identityGroup -Name $identityName
$certificate = Get-AzKeyVaultCertificate -VaultName $vaultName -Name $certificateName
$secretId = $certificate.SecretId
$versionlessSecretId = $secretId.Substring(0, $secretId.LastIndexOf('/') + 1)

Set-AzApplicationGatewayIdentity -ApplicationGateway $gateway -UserAssignedIdentityId $identity.Id | Out-Null
Add-AzApplicationGatewaySslCertificate -ApplicationGateway $gateway -Name $referenceName -KeyVaultSecretId $versionlessSecretId | Out-Null
Set-AzApplicationGateway -ApplicationGateway $gateway | Out-Null
```

The reference must end at `/secrets/<certificate-name>/`, with no version segment. Read access must remain valid for future versions. This enables automatic pickup; it does not guarantee immediate rollout. For the full supported procedure, see [Configure TLS termination using Azure PowerShell](https://learn.microsoft.com/azure/application-gateway/configure-keyvault-ps).

For a vault using the **Vault access policy** model, the Portal supports the initial selection: open **Listeners**, add or edit a listener, set **Protocol** to **HTTPS**, select **Create new** under **Choose a certificate**, then **Choose a certificate from Key Vault**. Enter the certificate reference name and select the managed identity, vault, and certificate. Verify the resulting reference is versionless.

## 4. Bind the HTTPS Listener and Route Traffic

1. In the gateway's **Listeners**, add or edit the intended listener. Select the public frontend, HTTPS, and port 443.
2. For a hostname-based listener, configure the custom hostname and SNI settings appropriate to your routing design. The hostname must be covered by the certificate.
3. Under **Choose a certificate**, select the gateway certificate reference added above. Save the listener.
4. Associate the listener with a request routing rule and the intended backend pool and backend settings. This guide configures client-to-gateway TLS; backend HTTPS and backend certificate validation are separate settings.
5. Create or update the hostname's public DNS A record to the gateway's static public frontend IP address. Check for an old CNAME or proxy that sends traffic elsewhere. For an existing production hostname, prepare and validate the listener and routing configuration before changing DNS.

## 5. Understand Rotation

After Acmebot merges a renewed certificate into the same Key Vault certificate name, Application Gateway polls Key Vault at **four-hour intervals** and installs the new certificate on its instances. A version-pinned reference continues to use the pinned version. A manually uploaded PFX has no ongoing Key Vault connection.

Microsoft also documents that a gateway configuration change triggers a Key Vault check. Treat any such change as a production configuration update, with normal review and rollback procedures. The four-hour polling interval is not an end-to-end rollout SLA: permissions, networking, certificate validity, and gateway provisioning must all succeed. See [supported certificates and rotation](https://learn.microsoft.com/azure/application-gateway/key-vault-certs#supported-certificates).

## 6. Verify the Public HTTPS Endpoint

From a client outside the gateway's network, open the custom HTTPS URL and inspect its certificate. Confirm the application response, hostname, trusted chain, and expiry. Compare the served leaf certificate's thumbprint or serial number with the current Key Vault certificate, both after setup and after a renewal has propagated.

With OpenSSL installed, replace `app.example.com` with your hostname and run:

```bash
openssl s_client -connect app.example.com:443 -servername app.example.com -verify_hostname app.example.com -verify_return_error -showcerts </dev/null
openssl s_client -connect app.example.com:443 -servername app.example.com </dev/null 2>/dev/null | openssl x509 -noout -subject -issuer -dates -serial -fingerprint -sha1
```

These examples use a POSIX shell, such as Bash or WSL, and OpenSSL's configured trusted CA store. The first command must complete certificate verification successfully; the second prints leaf metadata for comparison with the Key Vault thumbprint (ignore colon separators). SHA-1 here identifies the certificate; it is not a recommendation for certificate signatures. Do not disable verification to make a staging or mismatched certificate pass.

## Troubleshooting

| Symptom | Check and action |
| --- | --- |
| Key Vault is current but the old certificate is served | Confirm DNS reaches this gateway, the intended SNI listener uses this reference, and the reference is versionless. Allow for the polling interval and check gateway provisioning status. |
| Listener fails or becomes disabled | Check the user-assigned identity's secret read permission, role propagation, vault firewall/private DNS, enabled secret version, and exportable PFX. Revoking vault access can disable the listener. Inspect Azure Advisor and Resource Health. |
| Portal cannot select the certificate | For an RBAC vault, create the reference through PowerShell/CLI/IaC first. Check the operator's permissions separately from the gateway identity. |
| New versions never appear | Inspect the secret URI for a trailing version; do not use a `/certificates/` URI or an uploaded PFX when expecting automatic rotation. |
| Hostname or chain verification fails | Check SAN coverage, SNI hostname, DNS destination, and the CA chain. A wildcard covers one label only. Do not confuse the backend certificate with the public listener certificate. |

Use Microsoft's [common Key Vault errors](https://learn.microsoft.com/azure/application-gateway/application-gateway-key-vault-common-errors) and [disabled listener guidance](https://learn.microsoft.com/azure/application-gateway/disabled-listeners) for diagnosis. Monitor renewal through your own deployment's Application Insights and monitor the public endpoint separately; Key Vault success alone does not prove a successful TLS rollout.
