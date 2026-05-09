targetScope = 'resourceGroup'

@description('Azure region where the Function App, Storage account, Key Vault, and monitoring resources are deployed.')
param location string = resourceGroup().location

@description('Resource names for the deployed Acmebot components.')
param resourceNames resourceNamesType

@description('ACME account and directory settings.')
@secure()
param acme object

@description('DNS provider type and provider-specific settings.')
@secure()
param dnsProvider object

@description('Managed identity configuration for the Function App.')
param managedIdentity managedIdentitySettingsType = {
  type: 'SystemAssigned'
}

@description('Key Vault deployment target for certificates.')
param keyVault keyVaultSettingsType = {
  createNew: true
}

@description('Monitoring configuration for Application Insights and Log Analytics.')
param monitoring monitoringSettingsType = {
  createLogAnalyticsWorkspace: true
}

@description('Specifies whether the key vault is a standard vault or a premium vault.')
@allowed([
  'standard'
  'premium'
])
param keyVaultSkuName string = 'standard'

@description('Package URI deployed to the Function App.')
#disable-next-line no-hardcoded-env-urls
param appPackageUri string = 'https://stacmebotprod.blob.core.windows.net/acmebot/v5/latest.zip'

@description('Additional name/value pairs appended to the Function App app settings.')
param additionalAppSettings appSettingType[] = []

type appSettingType = {
  name: string
  value: string
}

type resourceNamesType = {
  functionAppName: string
  appServicePlanName: string
  storageAccountName: string
  keyVaultName: string
  appInsightsName: string
  logAnalyticsWorkspaceName: string
}

type managedIdentitySettingsType = {
  type: 'SystemAssigned' | 'UserAssigned'
  userAssignedResourceId: string?
  clientId: string?
}

type keyVaultSettingsType = {
  createNew: bool
  resourceId: string?
}

type monitoringSettingsType = {
  createLogAnalyticsWorkspace: bool
  logAnalyticsWorkspaceResourceId: string?
}

var functionAppName = resourceNames.functionAppName
var appServicePlanName = resourceNames.appServicePlanName
var appInsightsName = resourceNames.appInsightsName
var workspaceName = resourceNames.logAnalyticsWorkspaceName
var storageAccountName = resourceNames.storageAccountName
var newKeyVaultName = resourceNames.keyVaultName
var deploymentStorageContainerName = 'app-package-${toLower(functionAppName)}'

var createKeyVault = keyVault.createNew
var createLogAnalyticsWorkspace = monitoring.createLogAnalyticsWorkspace
var useUserAssignedIdentity = managedIdentity.type == 'UserAssigned'

var userAssignedIdentityResourceId = managedIdentity.?userAssignedResourceId ?? ''
var existingKeyVaultResourceId = keyVault.?resourceId ?? ''
var existingLogAnalyticsWorkspaceResourceId = monitoring.?logAnalyticsWorkspaceResourceId ?? ''

var userAssignedIdentityIdParts = split(userAssignedIdentityResourceId, '/')
var existingKeyVaultIdParts = split(existingKeyVaultResourceId, '/')

var selectedKeyVaultName = createKeyVault ? newKeyVaultName : last(existingKeyVaultIdParts)
var keyVaultBaseUrl = 'https://${selectedKeyVaultName}${environment().suffixes.keyvaultDns}'

var acmeEndpoint = acme.?endpoint ?? ''

var eabAppSettings = acme.?externalAccountBinding.?enabled == true ? [
  {
    name: 'Acmebot__ExternalAccountBinding__KeyId'
    value: acme.?externalAccountBinding.?keyId ?? ''
  }
  {
    name: 'Acmebot__ExternalAccountBinding__HmacKey'
    value: acme.?externalAccountBinding.?hmacKey ?? ''
  }
  {
    name: 'Acmebot__ExternalAccountBinding__Algorithm'
    value: acme.?externalAccountBinding.?algorithm ?? 'HS256'
  }
] : []

var akamaiDnsProvider = dnsProvider.?akamai ?? {}
var route53DnsProvider = dnsProvider.?route53 ?? {}
var azureDnsProvider = dnsProvider.?azureDns ?? {}
var azurePrivateDnsProvider = dnsProvider.?azurePrivateDns ?? {}
var cloudflareDnsProvider = dnsProvider.?cloudflare ?? {}
var customDnsProvider = dnsProvider.?customDns ?? {}
var dnsMadeEasyProvider = dnsProvider.?dnsMadeEasy ?? {}
var gandiLiveDnsProvider = dnsProvider.?gandiLiveDns ?? {}
var goDaddyProvider = dnsProvider.?goDaddy ?? {}
var googleDnsProvider = dnsProvider.?googleDns ?? {}
var ionosDnsProvider = dnsProvider.?ionosDns ?? {}
var regfishProvider = dnsProvider.?regfish ?? {}
var transIpProvider = dnsProvider.?transIp ?? {}
var unitedDomainsProvider = dnsProvider.?unitedDomains ?? {}
var dnsProviderType = dnsProvider.?type ?? ''

var dnsProviderAppSettings = dnsProviderType == 'Akamai' ? [
  {
    name: 'Acmebot__Akamai__Host'
    value: akamaiDnsProvider.?host ?? ''
  }
  {
    name: 'Acmebot__Akamai__ClientToken'
    value: akamaiDnsProvider.?clientToken ?? ''
  }
  {
    name: 'Acmebot__Akamai__ClientSecret'
    value: akamaiDnsProvider.?clientSecret ?? ''
  }
  {
    name: 'Acmebot__Akamai__AccessToken'
    value: akamaiDnsProvider.?accessToken ?? ''
  }
] : dnsProviderType == 'Route53' ? [
  {
    name: 'Acmebot__Route53__AccessKey'
    value: route53DnsProvider.?accessKey ?? ''
  }
  {
    name: 'Acmebot__Route53__SecretKey'
    value: route53DnsProvider.?secretKey ?? ''
  }
  {
    name: 'Acmebot__Route53__Region'
    value: route53DnsProvider.?region ?? 'us-east-1'
  }
] : dnsProviderType == 'AzureDns' ? [
  {
    name: 'Acmebot__AzureDns__SubscriptionId'
    value: azureDnsProvider.?subscriptionId ?? subscription().subscriptionId
  }
] : dnsProviderType == 'AzurePrivateDns' ? [
  {
    name: 'Acmebot__AzurePrivateDns__SubscriptionId'
    value: azurePrivateDnsProvider.?subscriptionId ?? subscription().subscriptionId
  }
] : dnsProviderType == 'Cloudflare' ? [
  {
    name: 'Acmebot__Cloudflare__ApiToken'
    value: cloudflareDnsProvider.?apiToken ?? ''
  }
] : dnsProviderType == 'CustomDns' ? [
  {
    name: 'Acmebot__CustomDns__Endpoint'
    value: customDnsProvider.?endpoint ?? ''
  }
  {
    name: 'Acmebot__CustomDns__ApiKey'
    value: customDnsProvider.?apiKey ?? ''
  }
  {
    name: 'Acmebot__CustomDns__ApiKeyHeaderName'
    value: customDnsProvider.?apiKeyHeaderName ?? 'X-Api-Key'
  }
  {
    name: 'Acmebot__CustomDns__PropagationSeconds'
    value: customDnsProvider.?propagationSeconds ?? '180'
  }
] : dnsProviderType == 'DnsMadeEasy' ? [
  {
    name: 'Acmebot__DnsMadeEasy__ApiKey'
    value: dnsMadeEasyProvider.?apiKey ?? ''
  }
  {
    name: 'Acmebot__DnsMadeEasy__SecretKey'
    value: dnsMadeEasyProvider.?secretKey ?? ''
  }
] : dnsProviderType == 'GandiLiveDns' ? [
  {
    name: 'Acmebot__GandiLiveDns__ApiKey'
    value: gandiLiveDnsProvider.?apiKey ?? ''
  }
] : dnsProviderType == 'GoDaddy' ? [
  {
    name: 'Acmebot__GoDaddy__ApiKey'
    value: goDaddyProvider.?apiKey ?? ''
  }
  {
    name: 'Acmebot__GoDaddy__ApiSecret'
    value: goDaddyProvider.?apiSecret ?? ''
  }
] : dnsProviderType == 'GoogleDns' ? [
  {
    name: 'Acmebot__GoogleDns__KeyFile64'
    value: googleDnsProvider.?keyFile64 ?? ''
  }
] : dnsProviderType == 'IonosDns' ? [
  {
    name: 'Acmebot__IonosDns__ApiKey'
    value: ionosDnsProvider.?apiKey ?? ''
  }
] : dnsProviderType == 'Regfish' ? [
  {
    name: 'Acmebot__Regfish__ApiKey'
    value: regfishProvider.?apiKey ?? ''
  }
] : dnsProviderType == 'TransIp' ? [
  {
    name: 'Acmebot__TransIp__CustomerName'
    value: transIpProvider.?customerName ?? ''
  }
  {
    name: 'Acmebot__TransIp__PrivateKeyName'
    value: transIpProvider.?privateKeyName ?? ''
  }
] : dnsProviderType == 'UnitedDomains' ? [
  {
    name: 'Acmebot__UnitedDomains__ApiKey'
    value: unitedDomainsProvider.?apiKey ?? ''
  }
] : []

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = if (useUserAssignedIdentity) {
  name: userAssignedIdentityIdParts[8]
  scope: resourceGroup(userAssignedIdentityIdParts[2], userAssignedIdentityIdParts[4])
}

var identityAppSettings = useUserAssignedIdentity ? [
  {
    name: 'Acmebot__ManagedIdentityClientId'
    value: managedIdentity.?clientId ?? userAssignedIdentity.?properties.clientId ?? ''
  }
] : []

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

var acmebotAppSettings = concat([
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsights.properties.ConnectionString
  }
  {
    name: 'AzureWebJobsStorage'
    value: storageConnectionString
  }
  {
    name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING'
    value: storageConnectionString
  }
  {
    name: 'Acmebot__Contacts'
    value: acme.?contacts ?? ''
  }
  {
    name: 'Acmebot__Endpoint'
    value: acmeEndpoint
  }
  {
    name: 'Acmebot__VaultBaseUrl'
    value: keyVaultBaseUrl
  }
  {
    name: 'Acmebot__Environment'
    value: environment().name
  }
], eabAppSettings, dnsProviderAppSettings, identityAppSettings, additionalAppSettings)

var functionPrincipalId = useUserAssignedIdentity ? userAssignedIdentity.?properties.principalId ?? '' : functionApp.identity.principalId

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

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = if (createLogAnalyticsWorkspace) {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

var workspaceResourceId = workspace.?id ?? existingLogAnalyticsWorkspaceResourceId

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: {
    'hidden-link:${resourceGroup().id}/providers/Microsoft.Web/sites/${functionAppName}': 'Resource'
  }
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceResourceId
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

var functionAppIdentity = useUserAssignedIdentity ? {
  type: 'UserAssigned'
  userAssignedIdentities: {
    '${userAssignedIdentityResourceId}': {}
  }
} : {
  type: 'SystemAssigned'
}

resource functionApp 'Microsoft.Web/sites@2025-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: functionAppIdentity
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
      appSettings: acmebotAppSettings
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
  #disable-next-line BCP187
  properties: {
    packageUri: appPackageUri
    remoteBuild: false
  }
}

resource newKeyVault 'Microsoft.KeyVault/vaults@2025-05-01' = if (createKeyVault) {
  name: newKeyVaultName
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

module newKeyVaultRoleAssignment 'keyvault_rbac.bicep' = if (createKeyVault) {
  params: {
    keyVaultName: newKeyVault.name
    principalId: functionPrincipalId
  }
}

module existingKeyVaultRoleAssignment 'keyvault_rbac.bicep' = if (!createKeyVault) {
  scope: resourceGroup(existingKeyVaultIdParts[2], existingKeyVaultIdParts[4])
  params: {
    keyVaultName: existingKeyVaultIdParts[8]
    principalId: functionPrincipalId
  }
}

output functionAppName string = functionApp.name
output principalId string = functionPrincipalId
output tenantId string = useUserAssignedIdentity ? subscription().tenantId : functionApp.identity.tenantId
output keyVaultName string = selectedKeyVaultName
output keyVaultBaseUrl string = keyVaultBaseUrl
output appInsightsName string = appInsights.name
output logAnalyticsWorkspaceResourceId string = workspaceResourceId
