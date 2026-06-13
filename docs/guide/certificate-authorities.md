# Certificate Authorities

Acmebot works with ACME v2 certificate authorities (CAs). Configure the ACME directory endpoint with `Acmebot__Endpoint`.

## Verified Endpoints

| CA | Endpoint | Notes |
| --- | --- | --- |
| Let's Encrypt | `https://acme-v02.api.letsencrypt.org/directory` | No EAB required. See the [Let's Encrypt documentation](https://letsencrypt.org/docs/) to get started. |
| GlobalSign | `https://emea.acme.atlas.globalsign.com/directory` | Requires a GlobalSign account with ACME enabled. See the [GlobalSign ACME documentation](https://docs.globalsign.com/solutions/services/clm/acme) for account setup. |
| Google Trust Services | `https://dv.acme-v02.api.pki.goog/directory` | EAB credentials are required. See the [Google Public CA documentation](https://cloud.google.com/certificate-manager/docs/public-ca-tutorial) for account setup. |
| SSL.com ECC | `https://acme.ssl.com/sslcom-dv-ecc` | Use when issuing ECC certificates through SSL.com. EAB credentials are typically required. |
| SSL.com RSA | `https://acme.ssl.com/sslcom-dv-rsa` | EAB credentials are typically required. See the [SSL.com ACME guide](https://www.ssl.com/guide/ssl-tls-certificate-issuance-and-revocation-with-acme/) for credential setup. |
| ZeroSSL | `https://acme.zerossl.com/v2/DV90` | EAB credentials are required. See the [ZeroSSL ACME documentation](https://zerossl.com/documentation/acme) for credential setup. |

You can also enter a custom ACME directory endpoint in the deployment form.

## Contact Email

Set the account contact email address:

```text
Acmebot__Contacts=admin@example.com
```

Enter the email address without the `mailto:` scheme. Acmebot adds it when calling the ACME API. Use a monitored address because some CAs send expiration or account notices there.

## External Account Binding

Some CAs require external account binding (EAB). Configure EAB before the first successful ACME account registration for that endpoint.

Treat the EAB key ID and HMAC key as CA-issued secrets. Do not configure EAB unless the selected ACME CA explicitly requires it.

Configure the EAB key ID, HMAC key, and algorithm:

```text
Acmebot__ExternalAccountBinding__KeyId=<key-id>
Acmebot__ExternalAccountBinding__HmacKey=<base64url-hmac-key>
Acmebot__ExternalAccountBinding__Algorithm=HS256
```

Supported algorithms depend on the CA. The deployment form supports `HS256`, `HS384`, and `HS512`.

Common EAB scenarios:

| CA | EAB guidance |
| --- | --- |
| Let's Encrypt | Not required for standard public ACME accounts. |
| GlobalSign | Required for an ACME-enabled GlobalSign account. |
| Google Trust Services | Required for ACME account registration. |
| SSL.com | Typically required. |
| ZeroSSL | Required for ACME account registration. |

## Preferred Chain

If the ACME server offers alternate certificate chains, use `PreferredChain` to select the chain whose root or issuer name matches your environment.

```text
Acmebot__PreferredChain=<issuer-or-root-name>
```

If no matching alternate chain is found, Acmebot uses the default chain returned by the CA.

## Preferred Profile

If the ACME server advertises certificate profiles, use `PreferredProfile` to request one.

```text
Acmebot__PreferredProfile=<profile-name>
```

Acmebot validates advertised profiles before using them. If the profile is not advertised by the directory, certificate issuance fails early.

## Staging and Production

Use a staging endpoint first when one is available. Staging validates DNS permissions, dashboard authentication, webhook delivery, and Key Vault access without consuming production issuance limits.

To move to production:

1. Change `Acmebot__Endpoint` to the production directory URL.
2. Restart the Function App.
3. Issue a new certificate.

Acmebot tags each certificate with the endpoint that issued it and renews only certificates for the currently configured endpoint, so staging and production certificates stay separate.

## Renewal Behavior

During scheduled renewal, Acmebot checks each managed certificate in Key Vault. When the ACME directory supports renewal information, it uses the server-provided renewal window; otherwise it renews when the remaining certificate lifetime is no more than `Acmebot__RenewBeforeExpiry` percent. See [Operations](./operations) for the full renewal schedule.

## CA Selection Guidance

- Use Let's Encrypt when you want a default path without EAB.
- Use a commercial CA when you need a specific trust provider, account workflow, compliance requirement, or support model.
- Match the CA's RSA or ECC endpoint to the key type you plan to issue.
- Keep EAB credentials in app settings and rotate them according to your CA's guidance.
