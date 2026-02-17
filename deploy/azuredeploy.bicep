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

var generatedToken = toLower(uniqueString(resourceGroup().id, location))

var functionAppName = 'func-${appNamePrefix}-${take(generatedToken, 4)}'
var appServicePlanName = 'plan-${appNamePrefix}-${take(generatedToken, 4)}'
var appInsightsName = 'appi-${appNamePrefix}-${take(generatedToken, 4)}'
var workspaceName = 'log-${appNamePrefix}-${take(generatedToken, 4)}'
var storageAccountName = 'st${generatedToken}func'
var keyVaultName = 'kv-${appNamePrefix}-${take(generatedToken, 4)}'
var deploymentStorageContainerName = 'app-package-${take(appNamePrefix, 32)}-${take(generatedToken, 7)}'

var roleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions/', 'a4417e6f-fecd-4de8-b567-7b0420556985')

var acmebotAppSettings = [
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsights.properties.ConnectionString
  }
  {
    name: 'AzureWebJobsStorage'
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
  }
  {
    name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING'
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
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
]

resource storageAccount 'Microsoft.Storage/storageAccounts@2025-06-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }

  resource blobServices 'blobServices' = {
    name: 'default'
    properties: {
      deleteRetentionPolicy: {}
    }
    resource deploymentContainer 'containers' = {
      name: deploymentStorageContainerName
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
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

resource appServicePlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: appServicePlanName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2025-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    clientAffinityEnabled: false
    httpsOnly: true
    serverFarmId: appServicePlan.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}${deploymentStorageContainerName}'
          authentication: {
            type: 'StorageAccountConnectionString'
            storageAccountConnectionStringName: 'DEPLOYMENT_STORAGE_CONNECTION_STRING'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 100
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      appSettings: concat(acmebotAppSettings, additionalAppSettings)
      minTlsVersion: '1.2'
      cors: {
        allowedOrigins: ['https://portal.azure.com']
        supportCredentials: false
      }
    }
  }
}

resource functionAppDeploy 'Microsoft.Web/sites/extensions@2025-03-01' = {
  parent: functionApp
  name: 'onedeploy'
  properties: {
    packageUri: 'https://stacmebotprod.blob.core.windows.net/keyvault-acmebot/v5/latest.zip'
    remoteBuild: false
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' = if (createWithKeyVault) {
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
  name: guid(keyVault.id, functionAppName, roleDefinitionId)
  properties: {
    roleDefinitionId: roleDefinitionId
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output functionAppName string = functionApp.name
output principalId string = functionApp.identity.principalId
output tenantId string = functionApp.identity.tenantId
output keyVaultName string = createWithKeyVault ? keyVault.name : ''
