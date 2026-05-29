targetScope = 'resourceGroup'

@description('Name of the existing Acmebot Function App to update.')
@minLength(1)
param functionAppName string

@description('Acmebot version to deploy. Use latest or a specific v5 version such as 5.0.0 or v5.0.0.')
@minLength(1)
param targetVersion string = 'latest'

var versionIsLatest = toLower(targetVersion) == 'latest'
var normalizedTargetVersion = startsWith(toLower(targetVersion), 'v')
  ? substring(targetVersion, 1, length(targetVersion) - 1)
  : targetVersion

#disable-next-line no-hardcoded-env-urls
var appPackageUri = versionIsLatest
  ? 'https://github.com/polymind-inc/acmebot/releases/latest/download/acmebot.zip'
  : 'https://github.com/polymind-inc/acmebot/releases/download/v${normalizedTargetVersion}/acmebot.zip'

resource functionApp 'Microsoft.Web/sites@2025-03-01' existing = {
  name: functionAppName
}

resource functionAppDeploy 'Microsoft.Web/sites/extensions@2025-03-01' = {
  parent: functionApp
  name: 'onedeploy'
  #disable-next-line BCP187
  properties: {
    packageUri: appPackageUri
    type: 'zip'
  }
}

output functionAppName string = functionApp.name
output targetVersion string = targetVersion
output appPackageUri string = appPackageUri
