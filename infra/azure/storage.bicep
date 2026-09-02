targetScope = 'resourceGroup'

@description('Short project name used for resource naming and tags.')
param projectName string = 'poineer'

@allowed([
  'dev'
  'test'
  'prod'
])
@description('Deployment environment name used for resource naming and tags.')
param environmentName string = 'prod'

@description('Azure region for the storage account.')
param location string = resourceGroup().location

@description('Blob container that stores published POIneer dataset artifacts.')
param datasetContainerName string = 'datasets'

@description('Optional resource tags.')
param tags object = {}

var normalizedProjectName = toLower(replace(projectName, '-', ''))
var normalizedEnvironmentName = toLower(replace(environmentName, '-', ''))
var uniqueSuffix = take(uniqueString(subscription().id, resourceGroup().id, projectName, environmentName), 8)
var storageAccountName = take('st${normalizedProjectName}${normalizedEnvironmentName}${uniqueSuffix}', 24)

resource datasetStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  tags: union(tags, {
    project: projectName
    environment: environmentName
    workload: 'dataset-storage'
    managedBy: 'bicep'
  })
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowCrossTenantReplication: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: datasetStorageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource datasetContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: datasetContainerName
  properties: {
    publicAccess: 'None'
  }
}

output storageAccountName string = datasetStorageAccount.name
output datasetContainerName string = datasetContainer.name
output blobEndpoint string = datasetStorageAccount.properties.primaryEndpoints.blob
