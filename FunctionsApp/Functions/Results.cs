using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using FunctionsApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FunctionsApp.Functions;

public class Results
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly TableClient _tableClient;

    public Results(BlobServiceClient blobServiceClient, TableServiceClient tableServiceClient)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BlobContainerName"));
        _tableClient = tableServiceClient.GetTableClient(Environment.GetEnvironmentVariable("TableName"));
    }

    [FunctionName("results")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
        HttpRequest req, ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        string id = req.Query["id"];
        BlobClient blobClient = _blobContainerClient.GetBlobClient($"converted_{id}.png");

        // Happy path
        if (await blobClient.ExistsAsync())
        {
            var blobStream = await blobClient.OpenReadAsync();
            return new FileStreamResult(blobStream, (await blobClient.GetPropertiesAsync()).Value.ContentType);
        }

        // Get status from table storage
        try
        {
            Response<StatusEntry> entry = await _tableClient.GetEntityAsync<StatusEntry>("status", id);
            Console.WriteLine(entry.Value.Status);
            return new OkObjectResult("The image is currently being processed.\nStatus: " + entry.Value.Status);
        }

        // If not found, return 404
        catch (RequestFailedException)
        {
            return new NotFoundObjectResult($"The image with id '{id}' does not exist.");
        }
    }
}
