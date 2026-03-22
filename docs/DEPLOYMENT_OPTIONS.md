# Deployment Options for Private Fork

This document describes two options for adapting the original open-source publish/deploy workflow to your own Azure environment when hosting the code in a private GitHub repository.

## Background

The original workflow ([`.github/workflows/publish.yml`](../.github/workflows/publish.yml)) publishes artifacts to the upstream project's infrastructure:

- Uploads `latest.zip` to Azure Storage account `stacmebotprod`
- Publishes Bicep modules to ACR `cracmebotprod.azurecr.io`
- The Bicep template ([`deploy/azuredeploy.bicep`](../deploy/azuredeploy.bicep)) references `https://stacmebotprod.blob.core.windows.net/keyvault-acmebot/v5/latest.zip` as the package URL

Neither of these targets is accessible from your environment, so the workflow must be adapted.

---

## Option 1: GitHub Actions with `azure/functions-action` (Recommended)

Deploy directly from the GitHub Actions workflow to your Azure Function App using the official [`azure/functions-action`](https://github.com/Azure/functions-action). No intermediate storage, package URLs, or SAS tokens are needed.

### Architecture

```
git push tag v* ──▶ GitHub Actions ──▶ Build & Publish ──▶ azure/functions-action ──▶ Azure Function App
                                                 │
                                                 └──▶ GitHub Release (for version tracking)
```

- **Bicep** handles infrastructure only (Function App, Key Vault, Storage, App Insights)
- **GitHub Actions** handles code deployment separately

### Changes Required

#### 1. `deploy/azuredeploy.bicep`

Remove the `functionAppDeploy` resource (the `onedeploy` extension) entirely — code deployment is no longer part of the infrastructure template.

```diff
- resource functionAppDeploy 'Microsoft.Web/sites/extensions@2025-03-01' = {
-   parent: functionApp
-   name: 'onedeploy'
-   properties: {
-     packageUri: 'https://stacmebotprod.blob.core.windows.net/keyvault-acmebot/v5/latest.zip'
-     remoteBuild: false
-   }
- }
```

#### 2. `deploy/azuredeploy.json`

Regenerate from the updated Bicep file:

```bash
az bicep build -f ./deploy/azuredeploy.bicep --outfile ./deploy/azuredeploy.json
```

#### 3. `.github/workflows/publish.yml`

Replace the `deploy` job. The updated workflow:

```yaml
name: Publish

on:
  push:
    tags: [ v* ]

env:
  DOTNET_VERSION: 10.0.x

jobs:
  publish:
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
    - uses: actions/checkout@v4

    - name: Use .NET ${{ env.DOTNET_VERSION }}
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Setup Version
      id: setup_version
      run: |
        FULL_VERSION=${GITHUB_REF/refs\/tags\/v/}
        echo "VERSION=${FULL_VERSION}" >> $GITHUB_OUTPUT

    - name: Publish Function app
      run: dotnet publish -c Release -r linux-x64 --no-self-contained -o ./dist -p:Version=${{ steps.setup_version.outputs.version }} ./src/Acmebot

    - name: Zip Function app
      run: 7z a -mx=9 latest.zip ./dist/* ./dist/.[^.]*

    - name: Create GitHub Release
      uses: softprops/action-gh-release@v2
      with:
        tag_name: v${{ steps.setup_version.outputs.version }}
        name: v${{ steps.setup_version.outputs.version }}
        files: latest.zip
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

  deploy:
    runs-on: ubuntu-latest
    needs: publish
    permissions:
      contents: read
      id-token: write
    environment: production
    steps:
    - uses: actions/checkout@v4

    - name: Use .NET ${{ env.DOTNET_VERSION }}
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Publish Function app
      run: dotnet publish -c Release -r linux-x64 --no-self-contained -o ./dist ./src/Acmebot

    - name: Azure Login
      uses: azure/login@v2
      with:
        client-id: ${{ secrets.AZURE_CLIENT_ID }}
        tenant-id: ${{ secrets.AZURE_TENANT_ID }}
        subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

    - name: Deploy to Azure Functions
      uses: azure/functions-action@v1
      with:
        app-name: ${{ vars.FUNCTION_APP_NAME }}
        package: ./dist
```

### Prerequisites

1. **Azure OIDC federated credentials** — configure a federated identity credential on an App Registration or User-Assigned Managed Identity, linked to your GitHub repo:
   - Set `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` as GitHub secrets
2. **Function App name** — set `FUNCTION_APP_NAME` as a GitHub Actions variable (Settings → Environments → production → Variables)
3. **GitHub environment** — create a `production` environment in repo settings for deployment protection rules (optional but recommended)

### Pros

- Clean separation: infrastructure (Bicep) vs. code deployment (GitHub Actions)
- No intermediate storage accounts or package URLs
- Works with private repos — no public URL required
- Full control over the build and deploy pipeline
- Well-documented, widely used pattern

### Cons

- Requires Azure credentials configured in GitHub
- The Function App must already exist before the deploy job runs (deploy Bicep first)

---

## Option 2: Azure Deployment Center (Managed by Azure)

Azure Functions has native GitHub integration. Azure automatically generates and manages a GitHub Actions workflow for you.

### Architecture

```
git push to branch ──▶ Azure-managed GitHub Actions workflow ──▶ Azure Function App
```

### Setup Steps

1. **In Azure Portal:** Go to your Function App → **Deployment Center**
2. **Source:** Select **GitHub**
3. **Authorize:** Connect your GitHub account
4. **Configure:**
   - Organization: your GitHub org
   - Repository: your private repo
   - Branch: `master` (or your release branch)
5. **Save:** Azure commits a workflow file to your repo automatically

Azure generates a `.github/workflows/` YAML file that:
- Builds your .NET project
- Deploys to the Function App using a publish profile or OIDC

### Changes Required

#### 1. `deploy/azuredeploy.bicep`

Same as Option 1 — remove the `functionAppDeploy` resource since deployment is handled externally.

#### 2. `.github/workflows/publish.yml`

You can either:
- **Remove it entirely** and let Azure manage the workflow, or
- **Keep it** for creating GitHub Releases only (version tracking) and let the Azure-managed workflow handle deployment

#### 3. `deploy/azuredeploy.json`

Regenerate from the updated Bicep file.

### Prerequisites

1. **Function App must exist** — deploy the Bicep template first to create the infrastructure
2. **GitHub connection** — authorize Azure to access your private GitHub repo via the portal

### Pros

- Zero workflow maintenance — Azure manages the CI/CD pipeline
- Simple portal-based setup
- Works with private repos natively
- Uses publish profile by default (no OIDC setup needed)

### Cons

- Less control over the build process
- The auto-generated workflow may conflict with your existing `publish.yml`
- Tied to a specific branch (not tag-based like the current workflow)
- Harder to customize (e.g., adding release creation, custom build steps)
- If you modify the auto-generated workflow, Azure may overwrite it on reconfiguration

---

## Comparison

| Aspect | Option 1: `functions-action` | Option 2: Deployment Center |
|---|---|---|
| **Control** | Full | Limited |
| **Setup effort** | Medium (OIDC + secrets) | Low (portal wizard) |
| **Maintenance** | You manage the workflow | Azure manages it |
| **Trigger** | Tag-based (`v*`) | Branch-based (push) |
| **Private repo** | Works (OIDC auth) | Works (publish profile) |
| **GitHub Release** | Yes (custom step) | No (would need separate workflow) |
| **Customization** | Fully customizable | Limited |
| **Infrastructure separation** | Clean (Bicep = infra only) | Clean (Bicep = infra only) |

### Recommendation

**Option 1** is recommended if you want full control, tag-based releases, and GitHub Release tracking.  
**Option 2** is recommended if you want the simplest possible setup and don't need version tagging.
