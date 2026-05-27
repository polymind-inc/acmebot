# Deployment

Acmebot v5 is the recommended deployment channel. It runs on Azure Functions Flex Consumption with the .NET isolated worker and stores ACME state in Azure Storage.

## Deploy to Azure

<div class="deploy-buttons">
  <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fazuredeploy_ui.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2FuiFormDefinition.json">Azure Public</a>
  <a class="secondary" href="https://portal.azure.cn/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fazuredeploy_ui.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2FuiFormDefinition.json">Azure China</a>
  <a class="secondary" href="https://portal.azure.us/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fazuredeploy_ui.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2FuiFormDefinition.json">Azure Government</a>
</div>

## Template Inputs

### Basics

The Basics step selects subscription, resource group, region, and resource names. The prefixed naming mode generates names such as `func-<prefix>`, `plan-<prefix>`, `kv-<prefix>`, `appi-<prefix>`, and `log-<prefix>`.

Use custom names when you need to align with an existing naming standard. The template validates the Azure naming rules before deployment.

### ACME

The ACME step configures:

- ACME directory endpoint.
- Contact email for the ACME account.
- Optional external account binding credentials.

Use the same endpoint consistently for renewals. Acmebot only renews certificates that were issued by Acmebot for the currently configured endpoint.

### DNS Provider

Choose exactly one provider in the deployment form. You can add more providers later by adding app settings manually.

Provider credentials are stored as Function App app settings. Treat them as secrets and rotate them using the DNS provider's normal process.

### Identity

The Function App can use either:

- A system-assigned managed identity, which is simplest for a single deployment.
- A user-assigned managed identity, useful when you want a stable identity across redeployments or multiple apps.

When using a user-assigned identity, Acmebot sets `Acmebot__ManagedIdentityClientId` so Azure SDK clients choose that identity.

### Key Vault

The template can create a new Key Vault or use an existing one. New vaults are created with Azure RBAC enabled. The template assigns the Function App identity the `Key Vault Certificates Officer` role on the selected vault.

For existing vaults, verify that:

- Azure RBAC is enabled or equivalent certificate permissions are present.
- The Function App identity can create, merge, update, read, and list certificates.
- Any downstream application identity has the permissions it needs to read or import the certificate.

### Monitoring

The template creates Application Insights and can create or reuse a Log Analytics workspace. Application Insights receives Function execution telemetry and outbound HTTP instrumentation through OpenTelemetry.

## Provisioned Resources

The standard deployment creates:

| Resource | Purpose |
| --- | --- |
| Function App | Hosts the dashboard, HTTP API, timers, and Durable Functions orchestrations. |
| Flex Consumption plan | Runs the Linux Function App on the `FC1` SKU. |
| Storage account | Stores Functions runtime data, deployment package data, and Acmebot state. |
| Blob container `acmebot-state` | Stores ACME account and nonce state when running in Azure. |
| Blob deployment container | Stores the package used by Flex Consumption deployment. |
| Application Insights | Collects runtime telemetry. |
| Log Analytics workspace | Backs the Application Insights resource. |
| Key Vault | Stores certificates when you choose to create a new vault. |

## App Settings

The template writes the required Acmebot settings automatically:

```text
Acmebot__Endpoint=<acme-directory-url>
Acmebot__Contacts=<contact-email>
Acmebot__VaultBaseUrl=<key-vault-url>
Acmebot__Environment=<AzureCloud|AzureChinaCloud|AzureUSGovernment>
```

It also writes the selected DNS provider settings and managed identity settings. For the full list, see [Configuration](../reference/configuration).

## Post-Deployment Steps

After deployment:

1. Assign DNS zone permissions to the Function App identity.
2. Enable App Service Authentication and require authenticated requests.
3. Open the dashboard and confirm certificates and DNS zones load successfully.
4. Issue a test certificate from a staging ACME endpoint or a low-risk domain.
5. Review Application Insights for failures or repeated retry messages.

## Updating an Existing Deployment

Use the update template to redeploy the application package while preserving existing resources and app settings.

[Update an existing deployment](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fupdate%2Fazuredeploy.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fupdate%2FuiFormDefinition.json)

The update flow asks for the existing Function App and package version. It does not recreate Key Vault, Storage, or monitoring resources.

## Terraform

Acmebot is also available from the Terraform Registry.

[Open the Terraform module](https://registry.terraform.io/modules/polymind-inc/acmebot/azurerm/latest)

Use Terraform when you need repeatable deployments, custom network or authentication configuration, or integration with an existing platform module.

## Deployment Checklist

- Use a staging ACME endpoint for the first validation when possible.
- Scope DNS provider credentials to the smallest practical set of zones.
- Use a user-assigned identity if you need stable identity across app recreation.
- Confirm Key Vault RBAC and DNS RBAC after deployment.
- Enable dashboard authentication before using the app.
- Configure webhook notifications if certificate operations should alert an operations channel.
