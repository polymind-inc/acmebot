# Migrating from v4 to v5

Acmebot v5 moves the application from the .NET in-process worker to the .NET isolated worker. Existing certificates, Key Vault, DNS provider settings, managed identity, and monitoring resources can stay in place.

Use the automatic migration unless your environment requires the same changes to be applied through an internal deployment pipeline.

## What the Migration Changes

The in-place migration updates only the existing Function App worker setting, .NET site runtime, Run From Package mode, and application package.

| Area | Typical v4 value | v5 value |
| --- | --- | --- |
| Function worker | `FUNCTIONS_WORKER_RUNTIME=dotnet` | `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` |
| .NET site runtime | `netFrameworkVersion=v8.0` | `netFrameworkVersion=v10.0` |
| Run From Package | `WEBSITE_RUN_FROM_PACKAGE=https://stacmebotprod.blob.core.windows.net/keyvault-acmebot/v4/latest.zip` | `WEBSITE_RUN_FROM_PACKAGE=1` |
| Package deployment | Package URL stored in app settings | GitHub Release asset deployed by ARM OneDeploy or ZIP deploy |
| `Acmebot:Endpoint` | `https://acme-v02.api.letsencrypt.org/` | `https://acme-v02.api.letsencrypt.org/directory` |

v5 does not store the package URL in `WEBSITE_RUN_FROM_PACKAGE`. The v5 GitHub Release asset URL is used only as the deployment source for the ARM template or `az webapp deploy`.

Unlike v4, the v5 ACME client requires the full directory URL in `Acmebot:Endpoint`. Update the setting to include the `/directory` path. For Let's Encrypt the correct value is `https://acme-v02.api.letsencrypt.org/directory`.

The migration preserves existing app settings. Do not remove `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING` or `WEBSITE_CONTENTSHARE` during an in-place migration; when those settings exist, v5 continues to use the existing Azure Files content share for ACME account state.

The migration template does not recreate the Function App, App Service plan, Storage account, Key Vault, Application Insights, or Log Analytics workspace. To move to the new v5 default architecture on Flex Consumption, deploy a new v5 environment instead of using the in-place migration.

## Before You Start

Plan a short maintenance window. The Function App restarts during migration. Allow any certificate operations in progress to finish before you start.

Before changing the app:

1. Confirm the existing v4 deployment is healthy.
2. Record the Function App name and resource group.
3. Export the current app settings and store the file securely because it may include DNS provider credentials.
4. Confirm the configured managed identities still have Key Vault and DNS provider permissions.
5. Keep the dashboard authentication configuration in place.

You can export the current app settings with Azure CLI:

```bash
az functionapp config appsettings list \
  --resource-group <resource-group> \
  --name <function-app-name> \
  --output json > acmebot-v4-appsettings.json
```

## Automatic Migration

The automatic migration uses the ARM template in `deploy/migrate`. The template asks for the existing Function App, preserves current app settings, switches `WEBSITE_RUN_FROM_PACKAGE` to `1`, applies the v5 worker and .NET site runtime settings, and deploys the latest package from GitHub Releases.

<div class="deploy-buttons">
  <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fmigrate%2Fazuredeploy.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fmigrate%2FuiFormDefinition.json">Azure Public</a>
</div>

### Portal Steps

1. Open the Azure Public migration template.
2. Select the subscription that contains the existing Function App.
3. Select the existing Acmebot v4 Function App.
4. Review and create the deployment.
5. Wait for the deployment to complete.
6. Restart the Function App if the portal does not show the app as restarted.

### Azure CLI

Run the template deployment against the resource group that contains the existing Function App:

```bash
az deployment group create \
  --resource-group <resource-group> \
  --template-uri https://raw.githubusercontent.com/polymind-inc/acmebot/master/deploy/migrate/azuredeploy.json \
  --parameters functionAppName=<function-app-name>
```

## Manual Migration

Manual migration is useful when the same changes must be applied through an internal deployment process.

1. Stop the Function App.
2. Preserve all existing Acmebot, DNS provider, Key Vault, Storage, Application Insights, authentication, and webhook settings.
3. Set the Function App runtime app settings. This replaces the v4 URL-based Run From Package setting with `WEBSITE_RUN_FROM_PACKAGE=1`:

```bash
az functionapp config appsettings set \
  --resource-group <resource-group> \
  --name <function-app-name> \
  --settings FUNCTIONS_WORKER_RUNTIME=dotnet-isolated WEBSITE_RUN_FROM_PACKAGE=1
```

4. Set the .NET site runtime:

```bash
az functionapp config set \
  --resource-group <resource-group> \
  --name <function-app-name> \
  --net-framework-version v10.0
```

5. Deploy the v5 package from the GitHub Release asset. The URL is used only as the deployment source and is not stored in `WEBSITE_RUN_FROM_PACKAGE`:

```bash
az webapp deploy \
  --resource-group <resource-group> \
  --name <function-app-name> \
  --src-url https://github.com/polymind-inc/acmebot/releases/latest/download/acmebot.zip \
  --type zip \
  --async true
```

6. Start or restart the Function App.

```bash
az functionapp restart \
  --resource-group <resource-group> \
  --name <function-app-name>
```

When adding new Acmebot app settings after migration, prefer the double-underscore form used by the v5 documentation, such as `Acmebot__Endpoint`. Existing `Acmebot:` settings can remain in an in-place migration.

## Verify the Migration

After the Function App starts:

1. Open the dashboard and confirm the footer shows an Acmebot v5 version.
2. Confirm existing certificates appear in the dashboard.
3. Confirm existing certificates issued by v4 are shown as Acmebot-managed certificates.
4. Load DNS zones from the dashboard.
5. Renew a low-risk certificate or issue a test certificate from a staging ACME endpoint.
6. Check Application Insights for startup errors, configuration validation failures, and DNS or Key Vault authorization failures.

## Roll Back

If the migrated app does not start and you need to return to v4, restore the app settings snapshot. To roll back manually while keeping the non-URL Run From Package mode, reapply the v4 runtime values and deploy the v4 package from the release you are rolling back to:

```bash
az functionapp config appsettings set \
  --resource-group <resource-group> \
  --name <function-app-name> \
  --settings FUNCTIONS_WORKER_RUNTIME=dotnet FUNCTIONS_INPROC_NET8_ENABLED=1 WEBSITE_RUN_FROM_PACKAGE=1

az functionapp config set \
  --resource-group <resource-group> \
  --name <function-app-name> \
  --net-framework-version v8.0

az webapp deploy \
  --resource-group <resource-group> \
  --name <function-app-name> \
  --src-url <v4-package-url> \
  --type zip \
  --async true

az functionapp restart \
  --resource-group <resource-group> \
  --name <function-app-name>
```

Review Application Insights and the Function App log stream before trying the migration again.
