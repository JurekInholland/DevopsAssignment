param location string = resourceGroup().location

param storageAccountName string = 'storage${uniqueString(resourceGroup().id)}'

param storageContainerName string = 'image-container'
param queueName string = 'image-queue'
param queueName2 string = 'process-queue'
param tableName string = 'imagetable'

param functionAppName string = 'function${uniqueString(resourceGroup().id)}'
param hostingPlanName string = 'hostingPlan${uniqueString(resourceGroup().id)}'

// Storage
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-02-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }

  resource blobService 'blobServices' = {
    name: 'default'
    properties: {
      cors: {
        corsRules: [
          {
            allowedOrigins: [
              '*'
            ]
            allowedMethods: [
              'GET'
              'PUT'
              'POST'
              'DELETE'
              'HEAD'
            ]
            allowedHeaders: [
              '*'
            ]
            exposedHeaders: [
              '*'
            ]
            maxAgeInSeconds: 3600
          }
        ]
      }
    }
    resource storagecontainer 'containers' = {
      name: storageContainerName
      properties: {
        publicAccess: 'None'
      }
    }
  }

  resource queueService 'queueServices' = {
    name: 'default'

    resource queue 'queues' = {
      name: queueName
      properties: {
        metadata: {}
      }
    }

    resource queue2 'queues' = {
      name: queueName2
      properties: {
        metadata: {}
      }
    }
  }
  resource tableService 'tableServices' = {
    name: 'default'

    resource table 'tables' = {
      name: tableName
    }
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource azureFunction 'Microsoft.Web/sites@2020-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        //     {
        //       name: 'AzureWebJobsDashboard'
        //       value: 'DefaultEndpointsProtocol=https;AccountName=storageAccountName;AccountKey=${listKeys('storageAccountID1', '2019-06-01').key1}'
        //     }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value}'
        }
        {
          name: 'ImageUploadQueueName'
          value: 'image-queue'
        }
        {
          name: 'BlobContainerName'
          value: 'image-container'
        }
        {
          name: 'TableName'
          value: 'imagetable'
        }

        //     {
        //       name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
        //       value: 'DefaultEndpointsProtocol=https;AccountName=storageAccountName;AccountKey=${listKeys('storageAccountID3', '2019-06-01').key1}'
        //     }
        //     {
        //       name: 'WEBSITE_CONTENTSHARE'
        //       value: toLower('name')
        //     }
        //     {
        //       name: 'FUNCTIONS_EXTENSION_VERSION'
        //       value: '~2'
        //     }
        //     {
        //       name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
        //       value: reference('insightsComponents.id', '2015-05-01').InstrumentationKey
        //     }
        //     {
        //       name: 'FUNCTIONS_WORKER_RUNTIME'
        //       value: 'dotnet'
        //     }
      ]
    }
  }
}
