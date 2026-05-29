# Security

Acmebot relies on Azure identity, Key Vault, DNS provider credentials, and dashboard authentication. This page summarizes the security model and recommended operational boundaries.

## Dashboard Authentication

The Function App serves the dashboard and API endpoints with anonymous trigger authorization, but the application code requires an authenticated user.

Configure App Service Authentication and require sign-in before requests reach the app. Microsoft Entra ID is the typical provider for Azure-hosted deployments.

The HTTP triggers are configured with anonymous trigger authorization so App Service Authentication can provide the authenticated user principal to the application. Do not rely on a Functions host key as the only protection for the dashboard or API in v5 deployments.

## App Roles

By default, any authenticated dashboard user can issue and revoke certificates. To require app roles for sensitive operations, set:

```text
Acmebot__RequireAppRoles=true
```

Then assign these roles in the application registration:

| Role | Allows |
| --- | --- |
| `Acmebot.IssueCertificate` | Issue and manually renew certificates. |
| `Acmebot.RevokeCertificate` | Revoke certificates. |

Listing certificates and DNS zones still requires authentication.

### App Role Setup Checklist

1. Add the roles to the Microsoft Entra application registration used by App Service Authentication.
2. Assign the roles to users, groups, or service principals that should operate Acmebot.
3. Set `Acmebot__RequireAppRoles=true` on the Function App.
4. Restart the Function App so the new configuration value is applied.
5. Confirm a caller without the role receives `403` for issue or revoke operations.

## Managed Identity

Use managed identity for Azure resource access and cross-cloud federation. By default, Acmebot uses the app-wide managed identity. Azure DNS, Azure Private DNS, Route 53 web identity federation, and Google Cloud DNS workload identity federation can override it with provider-specific user-assigned managed identity client IDs. If no client ID is configured, Azure SDK clients use the Function App system-assigned managed identity.

Recommended scopes:

| Resource | Permission |
| --- | --- |
| Key Vault | `Key Vault Certificates Officer` or equivalent certificate permissions. |
| Azure DNS zone | `DNS Zone Contributor` or a narrower custom role. |
| Azure Private DNS zone | `Private DNS Zone Contributor` or a narrower custom role. |
| Route 53 IAM role | Trust policy that allows the selected managed identity web identity token to assume the role. |
| Google Cloud service account | Workload identity principal binding that allows the selected managed identity token to impersonate the service account. |

Prefer assigning roles at the individual zone or vault scope rather than subscription scope.

## DNS Provider Credentials

External DNS provider credentials are stored as Function App app settings. Treat them as operational secrets.

Recommendations:

- Use provider API tokens instead of account-wide credentials when available.
- Scope tokens to the zones Acmebot manages.
- Grant only zone read and DNS record edit permissions.
- Use App Service Key Vault references for secret values when possible.
- Rotate credentials regularly.
- Restart the Function App after updating credentials.

## Key Vault

Acmebot stores private keys in Key Vault. It creates certificate operations and merges the issued public certificate chain into the pending operation.

Recommendations:

- Use Azure RBAC for new vaults.
- Restrict certificate read/export permissions to identities that need them.
- Keep purge protection and backup policies aligned with your organization's certificate recovery requirements.
- Use custom tags for ownership and cost tracking, but do not overwrite the reserved `Acmebot` tag.

## Network Access

Acmebot needs outbound access to:

- The configured ACME directory.
- DNS provider APIs.
- Azure Resource Manager when using Azure DNS providers.
- Azure Key Vault.
- The configured webhook endpoint, if any.
- DNS resolvers used for challenge verification.

When using private networking or restricted outbound access, allow these dependencies explicitly.

## Webhooks

Webhook URLs are secrets because they often include credentials or signed tokens. Store them in app settings and limit access to users who can manage Function App configuration.

Webhook delivery failures are logged but do not prevent certificate issuance from completing.
