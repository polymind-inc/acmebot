---
description: "DNS-PERSIST-01 preparation, operator-managed DNS authorization, ACME account key protection, and removal procedures."
---

# DNS-PERSIST-01

DNS-PERSIST-01 is an ACME validation method being standardized for persistent DNS authorization. See the [IETF draft](https://datatracker.ietf.org/doc/html/draft-ietf-acme-dns-persist) and [Let's Encrypt announcement](https://letsencrypt.org/2026/02/18/dns-persist-01) for the protocol and CA rollout plans.

## Current Support

Acmebot exposes ACME account information through the dashboard and API. Certificate issuance and renewal through DNS-PERSIST-01 are not supported yet. DNS-01 with a configured DNS provider remains the only issuance path; keep its DNS permissions in place.

Acmebot does not generate `_validation-persist` TXT values, verify published persistent records, or report their wildcard policy or expiration. The record format is still under revision. Publishing a record does not enable a new validation mode in Acmebot.

## DNS Change Management

Acmebot never creates, updates, or deletes `_validation-persist` records. Treat them as persistent issuance authorization and manage them through DNS change management, IaC, or an explicit DNS administrator approval process.

For future provisioning, identify the domain, CA, and ACME account being authorized, assign an owner, and record the approved scope and review date. Keep the DNS administrator's credentials in the DNS management workflow. DNS-PERSIST-01 is intended to allow issuance without DNS writes; that separation is not available in Acmebot's current DNS-01 workflow.

## Obtain Account Information

Open **Account** in the dashboard header and copy the values, or call [GET /api/account](/reference/api#show-acme-account):

```http
GET /api/account
Accept: application/json
```

| Value | Source and meaning |
| --- | --- |
| Account URI | Identifies the account registered with the configured CA. |
| Directory URL | The configured `Acmebot__Endpoint`; use it to identify the CA environment. |
| CAA identities | The CA's directory `caaIdentities` metadata; empty if absent. Confirm acceptable persistent-validation issuer names with the CA. |

The first successful request registers an account when none exists. Configure the contact email and any required external account binding settings first. Account information contains no private key material.

These values alone are not a ready-to-publish record. Draft revision `-02` introduces `issuerDomainNames`, `accountHashPrefix`, and an account URI hash using the account public key thumbprint. Acmebot's account response does not expose those additional inputs. Follow the [current draft](https://datatracker.ietf.org/doc/html/draft-ietf-acme-dns-persist) and the CA's instructions for syntax and availability; do not paste the raw account URI into a record based on an older example.

Review `policy=wildcard` explicitly before authorizing wildcard scope. If using `persistUntil`, track its expiration in the DNS management workflow; Acmebot currently provides no expiry warning.

## Protect the ACME Account Key

With persistent issuance authorization, the ACME account key becomes the primary sensitive asset. Anyone who obtains it can act as that account and may obtain certificates for domains with valid persistent authorization, subject to CA policy. The account key is separate from certificate private keys held in Key Vault.

Acmebot stores account key material in `account_key.json`, alongside `account.json`, grouped by the configured ACME endpoint's host:

| State store | Account key location |
| --- | --- |
| Blob storage, used by deployments without an Azure Files content share | `<endpoint-host>/account_key.json` in the `acmebot-state` container of the storage account configured by `AzureWebJobsStorage`. |
| File system, used when an Azure Files content share is configured | `%HOME%/data/.acmebot/<endpoint-host>/account_key.json`. |

Restrict access to the storage account, content share, backups, and Function App administrative tools. Protect backups with the same controls as the live key. Do not put the account key in DNS, provisioning scripts, source control, or logs. Keep the key and account metadata together when restoring state; deleting the key is not an account rotation procedure.

## Remove Authorization or Roll Back

1. Identify the `_validation-persist` record for the account, CA, and domain being retired. Preserve unrelated authorization records at the same name.
2. Remove it through the approved DNS management workflow and verify the authoritative DNS result.
3. Allow for DNS caches and the CA's permitted reuse of already validated authorization. Removal prevents fresh validation once the change is visible; it may not immediately prevent every issuance based on previously validated data.
4. Revoke already issued certificates separately when required. Deleting a DNS record does not revoke certificates.
5. For Acmebot, retain or restore the DNS-01 provider configuration and verify issuance and renewal through the existing workflow.

If the account key is compromised, remove affected authorizations and follow the CA's account recovery and certificate revocation procedures. Before publishing replacement records, confirm the replacement account and current protocol requirements with the CA.
