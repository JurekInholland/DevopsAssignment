# SSR Assignment

### Deploy the Bicep file
````bash
az group create --name ssrRG --location westeurope
az deployment group create --resource-group ssrRG --template-file main.bicep
````

### Clean up the resource group
````bash
az group delete --name ssrRG --yes
````

### The following endpoints are available
````
POST /api/upload
GET /api/results?id=id
GET /api/status?id=id
````
