targetScope = 'resourceGroup'

@description('Name of the existing Acmebot Function App to update.')
@minLength(1)
param functionAppName string

#disable-next-line no-hardcoded-env-urls
var appPackageUri = 'https://stacmebotprod.blob.core.windows.net/acmebot/v5/latest.zip'

resource functionApp 'Microsoft.Web/sites@2025-03-01' existing = {
  name: functionAppName
}

resource functionAppRuntimeConfig 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: functionApp
  name: 'web'
  properties: {
    netFrameworkVersion: 'v10.0'
  }
}

resource functionAppAppSettings 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: functionApp
  name: 'appsettings'
  properties: union(list('${functionApp.id}/config/appsettings', '2025-03-01').properties, {
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    WEBSITE_RUN_FROM_PACKAGE: '1'
  })
}

resource functionAppDeploy 'Microsoft.Web/sites/extensions@2025-03-01' = {
  parent: functionApp
  name: 'onedeploy'
  #disable-next-line BCP187
  properties: {
    packageUri: appPackageUri
    remoteBuild: false
  }
  dependsOn: [
    functionAppAppSettings
    functionAppRuntimeConfig
  ]
}
