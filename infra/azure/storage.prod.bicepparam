using './storage.bicep'

param projectName = 'poineer'
param environmentName = 'prod'
param location = 'germanywestcentral'
param datasetContainerName = 'datasets'
param tags = {
  application: 'POIneer'
  component: 'datasets'
}
