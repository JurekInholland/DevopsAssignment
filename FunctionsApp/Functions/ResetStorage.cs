using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FunctionsApp.Functions;

public class ResetStorage
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly QueueClient _queueClient;
    private readonly TableClient _tableClient;

    public ResetStorage(BlobServiceClient blobServiceClient, QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BlobContainerName"));
        _queueClient = queueServiceClient.GetQueueClient(Environment.GetEnvironmentVariable("ImageUploadQueueName"));
        _tableClient = tableServiceClient.GetTableClient(Environment.GetEnvironmentVariable("TableName"));
    }

    /// <summary>
    /// For debugging purposes only. Resets the storage account to a clean state.
    /// </summary>
    [FunctionName("DeleteStorageClients")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
    {
        // Reset blob container
        await _blobContainerClient.DeleteIfExistsAsync();
        await _queueClient.DeleteIfExistsAsync();
        // await _tableClient.DeleteAsync();

        return new OkObjectResult("Storage was deleted.");
    }

    [FunctionName("CreateStorageClients")]
    public async Task<IActionResult> CreateClients(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
    {
        // Reset blob container
        await _blobContainerClient.CreateIfNotExistsAsync();
        await _queueClient.CreateIfNotExistsAsync();
        // await _tableClient.CreateIfNotExistsAsync();

        return new OkObjectResult("Storage was created.");
    }
}
