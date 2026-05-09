targetScope = 'resourceGroup'

@description('Name of the Key Vault where the Function App identity needs certificate access.')
param keyVaultName string

@description('Object ID of the managed identity assigned to the Function App.')
param principalId string

var keyVaultCertificatesOfficerRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a4417e6f-fecd-4de8-b567-7b0420556985')

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' existing = {
  name: keyVaultName
}

resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, principalId, keyVaultCertificatesOfficerRoleDefinitionId)
  properties: {
    roleDefinitionId: keyVaultCertificatesOfficerRoleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
