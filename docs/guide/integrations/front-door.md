---
title: Front Door TLS Certificates
description: Use Acmebot certificates with Azure Front Door Standard or Premium, configure Key Vault access and Latest rotation, and verify HTTPS at the edge.
---

# Front Door TLS Certificates

Acmebot issues and renews a certificate in Key Vault. Azure Front Door Standard/Premium retrieves that certificate through a Front Door secret and deploys it to its edge locations. These are separate operations. See the [integration overview](../service-integration) for other Azure services.

## When to Use This Integration

Use a customer-managed certificate when you need a particular supported CA, a certificate shared with other services, or centralized ACME renewal through Key Vault.

If Front Door's native managed certificate meets your requirements, it is usually simpler: Front Door handles issuance, storage, and renewal. Check [managed certificate domain and renewal requirements](https://learn.microsoft.com/azure/frontdoor/domain#azure-front-door-managed-tls-certificates), especially for apex domains, wildcard domains, and CNAME chains. Do not assume a wildcard always requires Acmebot; current Front Door guidance includes managed wildcard certificates with renewal limitations.

## Prerequisites

- An Azure Front Door **Standard** or **Premium** profile with an endpoint, origin group, working origin, and route. This walkthrough does not cover Front Door classic.
- A working [Acmebot deployment](../getting-started) with DNS-01 permissions and write access to its Key Vault. The certificate vault must be in the **same subscription as the Front Door profile**.
- An enabled Key Vault **certificate object**, with an exportable RSA private key and a PFX backing secret (`application/x-pkcs12`). Front Door does **not** support EC/ECDSA certificates. Use Acmebot's RSA 2048 default for an initial deployment.
- A complete certificate chain, including the leaf and intermediates, rooted in a CA on the Microsoft Trusted CA List. An arbitrary custom ACME CA or staging CA is not sufficient. The CN or SAN must match the domain; `*.example.com` does not cover the apex or multiple subdomain levels.
- Permission to configure the profile and grant its managed identity read access to the certificate vault. Acmebot's identity and Front Door's identity have different responsibilities.
- DNS control for the application hostname, ownership validation when required, and ACME validation. Front Door domain validation and Acmebot's `_acme-challenge` DNS-01 records are separate.

See Microsoft's [HTTPS configuration requirements](https://learn.microsoft.com/azure/frontdoor/standard-premium/how-to-configure-https-custom-domain#use-your-own-certificate) and [certificate requirements](https://learn.microsoft.com/azure/frontdoor/domain#certificate-requirements) before issuing a certificate for this service.

## 1. Issue and Inspect the Certificate

Follow [Getting Started](../getting-started) through Key Vault verification, choosing RSA and a supported production CA. In Key Vault, confirm that the certificate's current version is enabled, has the correct hostname and chain, and has an exportable private key. Keep the same certificate name on renewal.

Front Door requires a Key Vault certificate object, rather than a standalone secret containing a PFX. Acmebot stores issued certificates as certificate objects. A **Front Door secret** is a Front Door resource referencing that certificate; it is not a new private key or a second issuance workflow.

## 2. Enable the Front Door Managed Identity

1. In the Front Door profile, open **Security > Identity**.
2. For the simple system-assigned path, select **System assigned**, set **Status** to **On**, and **Save**. Confirm creation and record the **Object (principal) ID**.
3. If your organization uses a user-assigned identity instead, attach it under **User assigned** and grant that identity access in the next step.

Follow [Use managed identities in Front Door](https://learn.microsoft.com/azure/frontdoor/managed-identity). The legacy app-registration access path is being deprecated; use managed identity for new configurations.

## 3. Grant Key Vault Access and Check Networking

Use the vault's current permission model:

- **Azure RBAC:** in the vault's **Access control (IAM)**, add a role assignment for **Key Vault Secrets User**, select **Managed identity**, and select the profile's identity. Assign at the dedicated certificate vault scope, not the subscription or resource group. This role reads the certificate's backing secret without granting write or administrator access.
- **Vault access policy:** create an access policy with **List** and **Get** under **Secret permissions** for the profile's managed identity, following the managed identity guide. Save it.

Keep the certificate vault dedicated to the application/environment where practical. If limiting a shared RBAC vault to an individual backing secret, validate discovery and rotation with that scope; the operator may need separate metadata permissions. See [Key Vault RBAC scope guidance](https://learn.microsoft.com/azure/key-vault/general/rbac-guide#best-practices-for-individual-keys-secrets-and-certificates-role-assignments).

For a vault with network restrictions, allow trusted Microsoft services to bypass the vault firewall as required by [Front Door's Key Vault access guidance](https://learn.microsoft.com/azure/frontdoor/domain#grant-access-to-azure-front-door). An RBAC role alone does not bypass network restrictions. Do not assume Front Door can reach a private-endpoint-only vault through the origin's network path; confirm that the documented trusted-service access path is available.

## 4. Create a Front Door Secret with Latest

1. In the Front Door profile, go to **Security > Secrets** and select **+ Add certificate**.
2. Select the Key Vault certificate issued by Acmebot.
3. Leave the certificate version as **Latest** and select **Add**. A fixed version requires manual updates on future renewals.
4. Wait for successful provisioning. Under **Secrets**, check that the certificate's **Access role** is **Managed identity**.

For the supported Portal procedure, see [Select the certificate for Front Door to deploy](https://learn.microsoft.com/azure/frontdoor/standard-premium/how-to-configure-https-custom-domain#select-the-certificate-for-azure-front-door-to-deploy).

## 5. Bind the Custom Domain and Route Traffic

1. Go to **Settings > Domains > + Add**. Select the appropriate domain type and DNS management, then enter or select your custom hostname.
2. For **HTTPS**, select **Bring Your Own Certificate (BYOC)**. For **Secret**, select the Front Door certificate reference created above, then add the domain.
3. Confirm domain validation is **Approved**. Front Door supports BYOC-based ownership validation when the certificate CN or SAN matches. If prompted for DNS validation, publish the exact `_dnsauth` TXT record and value shown by the Portal. For older domains awaiting approval, follow the **Validation State > Revalidate** procedure in the [custom domain guide](https://learn.microsoft.com/azure/frontdoor/standard-premium/how-to-add-custom-domain).
4. Select the domain's **Unassociated** link to open **Associate endpoint and routes**. Choose the intended endpoint and routes, then select **Associate**.
5. Use the **DNS state** information to configure the custom subdomain's CNAME to the Front Door endpoint. For an apex domain, follow the separate [apex DNS configuration](https://learn.microsoft.com/azure/frontdoor/apex-domain) rather than creating an invalid apex CNAME.
6. Confirm the domain and certificate deployment states are ready before sending production traffic, then verify HTTPS. Client-to-Front Door TLS is separate from [Front Door-to-origin TLS](https://learn.microsoft.com/azure/frontdoor/end-to-end-tls); do not disable origin certificate validation to solve a public certificate binding issue.

For an existing domain, open its certificate state to access **Certificate details**, choose BYOC and the intended secret, and select **Update**. Review the [certificate type switching behavior](https://learn.microsoft.com/azure/frontdoor/domain#switch-between-certificate-types) when migrating from a managed certificate.

## 6. Understand Rotation and Its Limits

Acmebot renews into the same Key Vault certificate name. With **Latest** selected and read/network access preserved, Front Door detects and deploys the new version automatically. Microsoft documents **up to 72 hours** for automatic deployment of a renewed customer-managed certificate or secret. This is different from the initial provisioning or certificate-type switching time. See [customer-managed certificate renewal](https://learn.microsoft.com/azure/frontdoor/domain#renew-customer-managed-tls-certificates).

A fixed version stays fixed. Microsoft directs you to **add a new certificate** when switching between a specific version and **Latest**; bind the domain to the new Front Door secret afterward. Do not assume editing the Key Vault certificate changes a pinned Front Door reference.

Allow the downstream deployment window when choosing certificate lifetimes and renewal timing. In particular, do not assume a short-lived ACME certificate is operationally suitable merely because issuance succeeds. Monitor the public endpoint through the rollout; a current Key Vault version does not prove every edge location has updated.

## 7. Verify the Public HTTPS Endpoint

From outside Azure, open your custom HTTPS URL and inspect the certificate. Confirm the expected application is served, the hostname and chain validate, and the served leaf's expiry and serial number or thumbprint match the intended Key Vault version. Repeat after renewal and from more than one network/location where practical because Front Door deploys globally.

With OpenSSL installed, replace `www.example.com` with your hostname:

```bash
openssl s_client -connect www.example.com:443 -servername www.example.com -verify_hostname www.example.com -verify_return_error -showcerts </dev/null
openssl s_client -connect www.example.com:443 -servername www.example.com </dev/null 2>/dev/null | openssl x509 -noout -subject -issuer -dates -serial -fingerprint -sha1
```

Run these in a POSIX shell, such as Bash or WSL, using OpenSSL's configured trusted CA store. Require successful verification from the first command; the second prints leaf metadata. Compare the SHA-1 fingerprint with Key Vault's certificate thumbprint, ignoring separators. This is an identifier comparison, not a recommendation to use SHA-1 signatures. Testing the origin hostname or the default `azurefd.net` hostname does not verify the custom domain's certificate.

## Troubleshooting

| Symptom | Check and action |
| --- | --- |
| Old certificate still served | Confirm the custom domain uses the correct Front Door secret, its version is **Latest**, and DNS reaches this profile. Check provisioning state and access; automatic renewal deployment can take up to 72 hours. Compare results from multiple locations. |
| Certificate cannot be added or renewed | Check same-subscription vault placement, the profile's identity permissions and **Managed identity** access role, network restrictions/trusted-service access, and the enabled certificate version. |
| Certificate rejected | Check RSA rather than EC/ECDSA, PFX content type, exportable private key, CN/SAN coverage, and the complete chain to a Microsoft-trusted CA. A standalone Key Vault secret or staging certificate is insufficient. |
| Domain validation pending | Check BYOC certificate coverage, use **Revalidate** where required, or publish the exact `_dnsauth` record requested. Acmebot's `_acme-challenge` record does not fulfill Front Door's TXT validation. |
| Hostname mismatch or wrong application | Check CNAME/apex DNS, endpoint and route association, the actual hostname sent via SNI, and wildcard scope. Inspect the public domain rather than the origin. |
| Binding uses a fixed version | Add a new Front Door certificate reference using **Latest**, bind it to the domain, and verify rollout before retiring the old reference. |

Use [Front Door domain states](https://learn.microsoft.com/azure/frontdoor/domain#domain-validation-states) to diagnose validation, and [Operations](../operations) to monitor Acmebot renewal in your own Azure environment. Keep public endpoint monitoring separate from issuance monitoring.
