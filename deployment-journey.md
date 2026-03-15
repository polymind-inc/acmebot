# KeyVault-Acmebot Deployment Journey

## Problem

The original deployment template used **Consumption Plan (Y1)** which requires Azure Files with storage account key authentication. The Azure subscription has an enforced policy (**SFI-ID4.2.1 Storage Accounts - Safe Secrets Standard**) that automatically sets `allowSharedKeyAccess: false` on all storage accounts, causing deployment failures.

## Root Cause

| Issue | Details |
|-------|---------|
| **Policy** | `StorageAccount_DisableLocalAuth_Modify` at management group level |
| **Effect** | Automatically disables shared key access on storage accounts |
| **Impact** | Consumption Plan requires Azure Files + shared keys, which is blocked |

### Why Consumption Plan Requires Shared Keys

The Consumption Plan (Y1) uses **Azure Files** for:
1. Storing function app code - deployment package is extracted to the file share
2. Dynamic scaling - new instances mount the same Azure Files share to access code
3. Shared state - all instances access the same files

**Azure Files does not support Managed Identity authentication for SMB file shares** - it only supports connection strings with storage account keys.

Reference: [Azure Functions storage considerations](https://learn.microsoft.com/en-us/azure/azure-functions/storage-considerations)

## Solutions Attempted

### Attempt 1: Flex Consumption Plan (FC1) — ❌ Failed

Changed from **Consumption (Y1)** to **Flex Consumption (FC1)** which uses Managed Identity and blob storage instead of file shares.

**Result:** Failed because Flex Consumption requires the **isolated worker model** with a `.azurefunctions` directory in the deployment package. The KeyVault-Acmebot v4 package uses the **in-process model**, which is incompatible.

```
InvalidPackageContentException: Package content validation failed: 
Cannot find required .azurefunctions directory at root level in the .zip package.
```

### Attempt 2: Premium Plan (EP1) with Linux — ❌ Failed

Tried using Premium Plan with Linux (`functionapp,linux`) and `linuxFxVersion: DOTNET|8.0`.

**Result:** Failed with assembly loading errors on Linux:

```
Error building configuration in an external startup class. 
Could not load file or assembly 'KeyVault.Acmebot, Version=4.3.1.0'
```

### Attempt 3: Premium Plan (EP1) with Windows — ✅ Success

Used **Premium Plan (EP1)** with **Windows** (`kind: 'functionapp'`), which:
- Supports in-process model
- Supports Managed Identity for storage authentication
- Uses `WEBSITE_RUN_FROM_PACKAGE` external URL deployment
- Does not require Azure Files with shared keys

## Final Solution: Windows Premium Plan with Managed Identity

### Key Configuration Changes

| Component | Original (Y1) | Final (EP1 Windows) |
|-----------|---------------|---------------------|
| **App Service Plan** | `Y1` / `Dynamic` | `EP1` / `ElasticPremium` |
| **Platform** | Windows | Windows |
| **Identity Type** | System-Assigned | User-Assigned Managed Identity |
| **Storage Auth** | Connection strings with keys | Managed Identity |
| **Storage Config** | `allowSharedKeyAccess: true` | `allowSharedKeyAccess: false` |
| **Worker Runtime** | `dotnet` | `dotnet` (in-process) |

### App Settings for Managed Identity Storage

```bicep
{
  name: 'AzureWebJobsStorage__accountName'
  value: storageAccountName
}
{
  name: 'AzureWebJobsStorage__credential'
  value: 'managedidentity'
}
{
  name: 'AzureWebJobsStorage__clientId'
  value: userAssignedIdentity.properties.clientId
}
```

### Storage Role Assignments (Least Privilege)

| Role | Role ID | Purpose |
|------|---------|---------|
| **Storage Blob Data Owner** | `b7e6dc6d-f1e8-4753-8033-0f276bb0955b` | Host storage |
| **Storage Blob Data Contributor** | `ba92f5b4-2d11-453d-a403-e96b0029c9fe` | Deployment storage |
| **Storage Queue Data Contributor** | `974c5e8b-45b9-4653-ba55-5f855dd0fb88` | Durable Functions task queues |
| **Storage Table Data Contributor** | `0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3` | Durable Functions orchestration history |

### Why User-Assigned Identity Instead of System-Assigned

User-Assigned Managed Identity is created **before** the Function App, allowing role assignments to be set up before the Function App tries to access storage. This avoids:
- Circular dependency issues
- Race conditions where the app starts before roles propagate

## Final Deployment Command

```powershell
az group create --name sslmgmtv46-rg --location australiaeast

az deployment group create `
  --resource-group sslmgmtv46-rg `
  --template-file deploy/azuredeploy_v4.bicep `
  --parameters appNamePrefix="sslmgmtv46" `
               mailAddress="nhitran@microsoft.com" `
               acmeEndpoint="https://acme-v02.api.letsencrypt.org/directory" `
               createWithKeyVault=true
```

## Deployment Result

✅ Successfully deployed to `sslmgmtv46-rg` with all 31 functions:

| Resource | Name |
|----------|------|
| **Function App** | `func-sslmgmtv46-vmj6` |
| **Key Vault** | `kv-sslmgmtv46-vmj6` |
| **Managed Identity** | `id-sslmgmtv46-vmj6` |
| **Storage Account** | `stvmj...func` |
| **App Service Plan** | `plan-sslmgmtv46-vmj6` (EP1 Windows) |
| **Application Insights** | `appi-sslmgmtv46-vmj6` |
| **Log Analytics Workspace** | `log-sslmgmtv46-vmj6` |

### Functions Deployed

All 31 Acmebot functions deployed successfully:
- `AddCertificate_HttpStart`, `GetCertificates_HttpStart`, `GetDnsZones_HttpStart`
- `RenewCertificate_HttpStart`, `RenewCertificates_Timer`, `RevokeCertificate_HttpStart`
- Durable orchestrators and activities for certificate lifecycle management
- `StaticPage_Serve` for the web dashboard

## Lessons Learned

| Lesson | Details |
|--------|---------|
| **Flex Consumption ≠ In-Process** | Flex Consumption only supports isolated worker model packages |
| **Linux In-Process Issues** | Some .NET in-process packages have assembly loading issues on Linux |
| **Premium Plan + Windows** | Best option for in-process .NET apps needing Managed Identity |
| **User-Assigned MI** | Avoids race conditions with role assignments |

## Cost Comparison

| Plan | Estimated Monthly Cost | Notes |
|------|----------------------|-------|
| Consumption (Y1) | ~$0-5 | Pay-per-execution, but blocked by policy |
| Premium (EP1) | ~$150+ | Always-on minimum instance, but works with MI |

## Next Steps

1. **Configure DNS Provider** — Add DNS provider settings (Azure DNS, Cloudflare, etc.) via `additionalAppSettings` or Azure Portal

2. **Assign DNS Provider Permissions** — If using Azure DNS, assign the Managed Identity (`id-sslmgmtv46-vmj6`) the "DNS Zone Contributor" role on your DNS zones

3. **Access the Dashboard** — Navigate to `https://func-sslmgmtv46-vmj6.azurewebsites.net` to access the Acmebot dashboard

## Deployed API Routes

The Function App exposes the following HTTP endpoints:

| Method | Route | Description | File |
|--------|-------|-------------|------|
| `GET` | `/api/certificates` | List all certificates in Key Vault | `GetCertificates.cs` |
| `POST` | `/api/certificate` | Request a new certificate | `AddCertificate.cs` |
| `POST` | `/api/certificate/{certificateName}/renew` | Renew an existing certificate | `RenewCertificate.cs` |
| `POST` | `/api/certificate/{certificateName}/revoke` | Revoke a certificate | `RevokeCertificate.cs` |
| `GET` | `/api/dns-zones` | List available DNS zones from configured providers | `GetDnsZones.cs` |
| `GET` | `/api/state/{instanceId}` | Get orchestration instance state (polling) | `GetInstanceState.cs` |
| `GET` | `/{*path}` | Static files (Dashboard UI) | `StaticPage.cs` |

### Base URL

```
https://func-sslmgmtv46-vmj6.azurewebsites.net
```

### Authentication

All routes use `AuthorizationLevel.Anonymous` but are protected by **Azure AD Easy Auth** configured via `Acmebot:AllowedAppRoles` setting. Users must authenticate via Microsoft Entra ID to access the dashboard and API.

## Known Limitation

**Private DNS zones cannot be used with public ACME CAs** like Let's Encrypt. The zone `sslmgmt.kat` uses `.kat` which is not a valid public TLD. To issue certificates, you need:
- A real public domain (e.g., `example.com`)
- Azure Public DNS or another supported DNS provider
- DNS Zone Contributor permissions for the Managed Identity

## References

- [Azure Functions storage considerations](https://learn.microsoft.com/en-us/azure/azure-functions/storage-considerations)
- [Azure Functions Premium plan](https://learn.microsoft.com/en-us/azure/azure-functions/functions-premium-plan)
- [Connecting to host storage with an identity](https://learn.microsoft.com/en-us/azure/azure-functions/functions-reference#connecting-to-host-storage-with-an-identity)
- [KeyVault-Acmebot GitHub](https://github.com/shibayan/keyvault-acmebot)
