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
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;

namespace FunctionsApp.Functions;

public class Results
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly TableClient _tableClient;
    static readonly string[] scopeRequiredByApi = new string[] { "access_as_user" };

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


        var (authenticationStatus, authenticationResponse) =
            await req.HttpContext.AuthenticateAzureFunctionAsync();
        if (!authenticationStatus) return authenticationResponse;

        req.HttpContext.VerifyUserHasAnyAcceptedScope(scopeRequiredByApi);



        string id = req.Query["id"];

        if (id == null)
        {
            return new BadRequestObjectResult("No id was provided.");
        }

        BlobClient blobClient = _blobContainerClient.GetBlobClient($"converted_{id}.png");

        // Happy path
        if (await blobClient.ExistsAsync())
        {
            var blobStream = await blobClient.OpenReadAsync();
            return new FileStreamResult(blobStream, (await blobClient.GetPropertiesAsync()).Value.ContentType);
        }

        // Get status from table storage if blob doesn't exist yet
        try
        {
            Response<StatusEntry> entry = await _tableClient.GetEntityAsync<StatusEntry>("status", id);
            Console.WriteLine(entry.Value.Status);
            return new OkObjectResult($"The image is being processed.\nCurrent status: {entry.Value.Status}");
        }

        // If no status entry is found, return 404
        // This should only happen if an incorrect id is provided
        catch (RequestFailedException)
        {
            return new NotFoundObjectResult($"The image with id '{id}' does not exist.");
        }
    }
}
