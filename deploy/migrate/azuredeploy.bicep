targetScope = 'resourceGroup'

@description('Name of the existing Acmebot Function App to update.')
@minLength(1)
param functionAppName string

#disable-next-line no-hardcoded-env-urls
var appPackageUri = 'https://github.com/polymind-inc/acmebot/releases/latest/download/acmebot.zip'

resource functionApp 'Microsoft.Web/sites@2025-03-01' existing = {
  name: functionAppName
}

var existingAppSettings = list('${functionApp.id}/config/appsettings', '2025-03-01').properties
var preservedAppSettings = toObject(
  filter(items(existingAppSettings), setting => setting.key != 'FUNCTIONS_INPROC_NET8_ENABLED'),
  setting => setting.key,
  setting => setting.value
)
var migratedAppSettings = union(preservedAppSettings, {
  FUNCTIONS_EXTENSION_VERSION: '~4'
  FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
  WEBSITE_RUN_FROM_PACKAGE: '1'
})

resource functionAppRuntimeConfig 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: functionApp
  name: 'web'
  properties: {
    netFrameworkVersion: 'v10.0'
  }
}

module functionAppAppSettings 'appsettings.bicep' = {
  params: {
    functionAppName: functionApp.name
    appSettings: migratedAppSettings
  }
}

resource functionAppDeploy 'Microsoft.Web/sites/extensions@2025-03-01' = {
  parent: functionApp
  name: 'onedeploy'
  #disable-next-line BCP187
  properties: {
    packageUri: appPackageUri
    type: 'zip'
  }
  dependsOn: [
    functionAppAppSettings
    functionAppRuntimeConfig
  ]
}
