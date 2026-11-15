targetScope = 'resourceGroup'

@description('Existing workload profiles environment in the dev resource group.')
param environmentName string

@description('Azure region of the existing Container Apps environment.')
param location string

@description('Existing dev container registry in the same resource group.')
param registryName string

@description('Renderer repository and immutable digest, for example poineer-render@sha256:<digest>.')
param rendererImage string

@description('Existing dev storage account with private regions and metadata containers.')
param storageAccountName string = 'poineerstoragedev'

param jobName string = 'poineer-render-berlin-dev'

resource environment 'Microsoft.App/managedEnvironments@2025-01-01' existing = {
  name: environmentName
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' existing = {
  parent: storage
  name: 'default'
}

resource artifacts 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' existing = {
  parent: blobs
  name: 'regions'
}

resource metadata 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' existing = {
  parent: blobs
  name: 'metadata'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${jobName}-identity'
  location: location
}

var blobContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var acrPullRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

resource artifactAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(artifacts.id, identity.id, blobContributorRole)
  scope: artifacts
  properties: {
    roleDefinitionId: blobContributorRole
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource metadataAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(metadata.id, identity.id, blobContributorRole)
  scope: metadata
  properties: {
    roleDefinitionId: blobContributorRole
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource imageAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, identity.id, acrPullRole)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRole
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

var scratch = '/tmp/poineer-berlin'
var rendererSettings = {
  Renderer__OnlyRegionId: 'geofabrik/europe/germany/berlin'
  Renderer__WorkDir: '${scratch}/work'
  Renderer__OutDir: '${scratch}/out'
  Renderer__LockFilePath: '${scratch}/renderer.lock'
  VectorTiles__Enabled: 'true'
  VectorTiles__AdditionalArguments__0: '--download'
  VectorTiles__AdditionalArguments__1: '--water-polygons-url=https://ftp.uni-erlangen.de/pub/openstreetmapdata.com-archive/2026-04-26/water-polygons-split-3857.zip'
  Publisher__Target: 'AzureBlob'
  AzureBlobPublisher__AccountName: storage.name
  AzureBlobPublisher__ContainerName: artifacts.name
  AzureBlobPublisher__MaxUploadsPerRun: '2'
  MetadataPublisher__Target: 'AzureBlob'
  MetadataPublisher__AccountName: storage.name
  MetadataPublisher__ContainerName: metadata.name
}

resource job 'Microsoft.App/jobs@2025-01-01' = {
  name: jobName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'renderer'
          image: '${registry.properties.loginServer}/${rendererImage}'
          resources: {
            cpu: 4
            memory: '8Gi'
          }
          env: concat([
            {
              name: 'AZURE_CLIENT_ID'
              value: identity.properties.clientId
            }
            {
              name: 'RENDER_SCRATCH'
              value: scratch
            }
            {
              name: 'TMPDIR'
              value: scratch
            }
          ], map(items(rendererSettings), setting => {
            name: 'POINEER_RENDER__${setting.key}'
            value: setting.value
          }))
          command: [
            '/bin/sh'
          ]
          args: [
            '-ec'
            '''
mkdir -p "$RENDER_SCRATCH"
cd "$RENDER_SCRATCH"

sample_disk() {
  used=$(du -sk . | cut -f 1)
  printf 'scratch_kib=%s\n' "$used"
}

(
  while :; do
    sample_disk
    sleep 5
  done
) &
sampler=$!
trap 'kill "$sampler" 2>/dev/null || true' EXIT

status=0
dotnet /opt/poineer-render/app/POIneer.Render.dll || status=$?
sample_disk
exit "$status"
'''
          ]
        }
      ]
    }
  }
  dependsOn: [
    artifactAccess
    metadataAccess
    imageAccess
  ]
}

output jobResourceId string = job.id
