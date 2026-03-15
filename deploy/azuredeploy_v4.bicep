@description('The name of the function app that you wish to create.')
@maxLength(14)
param appNamePrefix string

@description('The location of the function app that you wish to create.')
param location string = resourceGroup().location

@description('Email address for ACME account.')
param mailAddress string

@description('Certification authority ACME Endpoint.')
@allowed([
  'https://acme-v02.api.letsencrypt.org/directory'
  'https://acme.zerossl.com/v2/DV90/'
  'https://dv.acme-v02.api.pki.goog/directory'
  'https://acme.entrust.net/acme2/directory'
  'https://emea.acme.atlas.globalsign.com/directory'
])
param acmeEndpoint string = 'https://acme-v02.api.letsencrypt.org/directory'

@description('If you choose true, create and configure a key vault at the same time.')
@allowed([
  true
  false
])
param createWithKeyVault bool = true

@description('Specifies whether the key vault is a standard vault or a premium vault.')
@allowed([
  'standard'
  'premium'
])
param keyVaultSkuName string = 'standard'

@description('Enter the base URL of an existing Key Vault. (ex. https://example.vault.azure.net)')
param keyVaultBaseUrl string = ''

@description('Specifies additional name/value pairs to be appended to the functionap app appsettings.')
param additionalAppSettings array = []

var functionAppName = 'func-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var appServicePlanName = 'plan-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var appInsightsName = 'appi-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var workspaceName = 'log-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var storageAccountName = 'st${uniqueString(resourceGroup().id, deployment().name)}func'
var keyVaultName = 'kv-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var userAssignedIdentityName = 'id-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var keyVaultRoleDefinitionId = resourceId('Microsoft.Authorization/roleDefinitions/', 'a4417e6f-fecd-4de8-b567-7b0420556985')
// Storage Blob Data Owner role - Required for host storage and deployment package
var storageBlobDataOwnerRoleId = resourceId('Microsoft.Authorization/roleDefinitions/', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
// Storage Blob Data Contributor role - Required for Flex Consumption deployment storage
var storageBlobDataContributorRoleId = resourceId('Microsoft.Authorization/roleDefinitions/', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
// Storage Table Data Contributor role - Required for Durable Functions orchestration history
var storageTableDataContributorRoleId = resourceId('Microsoft.Authorization/roleDefinitions/', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
// Storage Queue Data Contributor role - Required for Durable Functions task coordination
var storageQueueDataContributorRoleId = resourceId('Microsoft.Authorization/roleDefinitions/', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var acmebotAppSettings = [
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsights.properties.ConnectionString
  }
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
  {
    name: 'WEBSITE_RUN_FROM_PACKAGE'
#disable-next-line no-hardcoded-env-urls
    value: 'https://stacmebotprod.blob.core.windows.net/keyvault-acmebot/v4/latest.zip'
  }
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet'
  }
  {
    name: 'FUNCTIONS_INPROC_NET8_ENABLED'
    value: '1'
  }
  {
    name: 'Acmebot__Contacts'
    value: mailAddress
  }
  {
    name: 'Acmebot__Endpoint'
    value: acmeEndpoint
  }
  {
    name: 'Acmebot__VaultBaseUrl'
    value: (createWithKeyVault ? 'https://${keyVaultName}${environment().suffixes.keyvaultDns}' : keyVaultBaseUrl)
  }
  {
    name: 'Acmebot__Environment'
    value: environment().name
  }
  {
    name: 'Acmebot__ManagedIdentityClientId'
    value: userAssignedIdentity.properties.clientId
  }
]

resource storageAccount 'Microsoft.Storage/storageAccounts@2025-01-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// User-Assigned Managed Identity for Flex Consumption Function App
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: userAssignedIdentityName
  location: location
}

// Storage role assignments for User-Assigned Managed Identity (must be created before Function App)
resource storageBlobDataOwnerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, userAssignedIdentityName, storageBlobDataOwnerRoleId)
  properties: {
    roleDefinitionId: storageBlobDataOwnerRoleId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, userAssignedIdentityName, storageBlobDataContributorRoleId)
  properties: {
    roleDefinitionId: storageBlobDataContributorRoleId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageTableDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, userAssignedIdentityName, storageTableDataContributorRoleId)
  properties: {
    roleDefinitionId: storageTableDataContributorRoleId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageQueueDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, userAssignedIdentityName, storageQueueDataContributorRoleId)
  properties: {
    roleDefinitionId: storageQueueDataContributorRoleId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: appServicePlanName
  location: location
  kind: 'elastic'
  sku: {
    name: 'EP1'
    tier: 'ElasticPremium'
  }
  properties: {
    maximumElasticWorkerCount: 20
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: {
    'hidden-link:${resourceGroup().id}/providers/Microsoft.Web/sites/${functionAppName}': 'Resource'
  }
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource functionApp 'Microsoft.Web/sites@2024-11-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    clientAffinityEnabled: false
    httpsOnly: true
    serverFarmId: appServicePlan.id
    keyVaultReferenceIdentity: userAssignedIdentity.id
    siteConfig: {
      appSettings: concat(acmebotAppSettings, additionalAppSettings)
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      cors: {
        allowedOrigins: ['https://portal.azure.com']
        supportCredentials: false
      }
    }
  }
  dependsOn: [
    storageBlobDataOwnerRoleAssignment
    storageBlobDataContributorRoleAssignment
    storageTableDataContributorRoleAssignment
    storageQueueDataContributorRoleAssignment
  ]
}

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = if (createWithKeyVault) {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: keyVaultSkuName
    }
    enableRbacAuthorization: true
  }
}

resource keyVault_roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (createWithKeyVault) {
  scope: keyVault
  name: guid(keyVault.id, userAssignedIdentityName, keyVaultRoleDefinitionId)
  properties: {
    roleDefinitionId: keyVaultRoleDefinitionId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output functionAppName string = functionApp.name
output principalId string = userAssignedIdentity.properties.principalId
output tenantId string = userAssignedIdentity.properties.tenantId
output keyVaultName string = createWithKeyVault ? keyVault.name : ''
