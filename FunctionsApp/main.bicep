﻿param location string = resourceGroup().location

param storageAccountName string = 'storage${uniqueString(resourceGroup().id)}'

param storageContainerName string = 'image-container'
param queueName string = 'image-queue'

param tableName string = 'imagetable'

param functionAppName string = 'function${uniqueString(resourceGroup().id)}'
param hostingPlanName string = 'hostingPlan${uniqueString(resourceGroup().id)}'

resource storageaccount 'Microsoft.Storage/storageAccounts@2021-02-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

resource storagecontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-02-01' = {
  name: '${storageAccountName}/default/${storageContainerName}'
  properties: {
    publicAccess: 'None'
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2022-05-01' = {
  name: '${storageAccountName}/default'
}
resource queue 'Microsoft.Storage/storageAccounts/queueServices/queues@2022-05-01' = {
  name: queueName
  parent: queueService
  properties: {
    metadata: {}
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2021-02-01' = {
  name: '${storageAccountName}/default'
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
}
resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2022-05-01' = {
  name: 'default'
  parent: storageaccount
}

resource table 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-05-01' = {
  name: tableName
  parent: tableService
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
    // siteConfig: {
    //   appSettings: [
    //     {
    //       name: 'AzureWebJobsDashboard'
    //       value: 'DefaultEndpointsProtocol=https;AccountName=storageAccountName;AccountKey=${listKeys('storageAccountID1', '2019-06-01').key1}'
    //     }
    //     {
    //       name: 'AzureWebJobsStorage'
    //       value: 'DefaultEndpointsProtocol=https;AccountName=storageAccountName;AccountKey=${listKeys('storageAccountID2', '2019-06-01').key1}'
    //     }
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
    //   ]
    // }
  }
}
