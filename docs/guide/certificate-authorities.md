# Certificate Authorities

Acmebot works with ACME v2 certificate authorities. Configure the ACME directory endpoint with `Acmebot__Endpoint`.

## Known Endpoints

| CA | Endpoint | Notes |
| --- | --- | --- |
| Let's Encrypt | `https://acme-v02.api.letsencrypt.org/directory` | No EAB required. |
| ZeroSSL | `https://acme.zerossl.com/v2/DV90` | EAB credentials are required. |
| Google Trust Services | `https://dv.acme-v02.api.pki.goog/directory` | EAB may be required depending on your account setup. |
| SSL.com RSA | `https://acme.ssl.com/sslcom-dv-rsa` | EAB credentials are typically required. |
| SSL.com ECC | `https://acme.ssl.com/sslcom-dv-ecc` | Use when issuing ECC certificates through SSL.com. |
| Entrust | `https://acme.entrust.net/acme2/directory` | Requires an Entrust ACME-enabled account. |
| GlobalSign Atlas | `https://emea.acme.atlas.globalsign.com/directory` | Requires an Atlas ACME-enabled account. |

You can also enter a custom ACME directory endpoint in the deployment form.

## Contact Email

Set one or more account contacts:

```text
Acmebot__Contacts=mailto:admin@example.com
```

The value is passed to the ACME new-account request. Use a monitored address because some CAs send expiration or account notices there.

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
| Let's Encrypt | Usually not required. |
| Buypass | Usually not required. |
| ZeroSSL | Required for ACME account registration. |
| Google Trust Services | May be required depending on account setup. |
| SSL.com | Typically required. |
| Entrust | Required for an ACME-enabled Entrust account. |

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

Use a staging endpoint first when available. Staging is useful for validating DNS permissions, dashboard authentication, webhook delivery, and Key Vault access without consuming production issuance limits.

When moving to production:

1. Change `Acmebot__Endpoint` to the production directory URL.
2. Restart the Function App.
3. Issue a new certificate.

Acmebot renews only certificates tagged for the currently configured endpoint, so staging certificates and production certificates are treated separately.

## Renewal Behavior

During scheduled renewal, Acmebot checks each managed certificate in Key Vault. If the ACME directory supports renewal information, Acmebot uses the server-provided suggested renewal window. Otherwise, it renews when the certificate expires within `Acmebot__RenewBeforeExpiry` days.

## CA Selection Guidance

- Use Let's Encrypt for a simple default path without EAB.
- Use a commercial CA when your organization requires a specific trust provider, account workflow, or support model.
- Use the CA's RSA or ECC endpoint consistently with the key type you plan to issue.
- Keep EAB credentials in app settings and rotate them according to your CA's guidance.
