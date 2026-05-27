targetScope = 'resourceGroup'

@description('Name of the existing Acmebot Function App to update.')
@minLength(1)
param functionAppName string

@description('Acmebot major version channel to deploy.')
@allowed([
  'v5'
])
param majorVersion string = 'v5'

@description('Acmebot version to deploy. Use latest or a specific v5 version such as 5.0.0 or v5.0.0.')
@minLength(1)
param targetVersion string = 'latest'

var versionIsLatest = toLower(targetVersion) == 'latest'
var normalizedTargetVersion = startsWith(toLower(targetVersion), 'v')
  ? substring(targetVersion, 1, length(targetVersion) - 1)
  : targetVersion
var packageFileName = versionIsLatest ? 'latest.zip' : '${normalizedTargetVersion}.zip'

#disable-next-line no-hardcoded-env-urls
var appPackageUri = 'https://stacmebotprod.blob.core.windows.net/acmebot/${majorVersion}/${packageFileName}'

resource functionApp 'Microsoft.Web/sites@2025-03-01' existing = {
  name: functionAppName
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

output functionAppName string = functionApp.name
output majorVersion string = majorVersion
output targetVersion string = targetVersion
output appPackageUri string = appPackageUri
