using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using FunctionsApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FunctionsApp;

public class StatusCheck
{
    private readonly TableClient _tableClient;

    public StatusCheck(TableServiceClient tableServiceClient)

    {
        _tableClient = tableServiceClient.GetTableClient(Environment.GetEnvironmentVariable("TableName"));
    }

    [FunctionName("status")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
        HttpRequest req, ILogger log)
    {
        try
        {
            Response<StatusEntry> response = await _tableClient.GetEntityAsync<StatusEntry>("status", req.Query["id"]);
            return new OkObjectResult("status: " + response.Value.Status);
        }
        catch (RequestFailedException)
        {
            return new BadRequestObjectResult("The given resource was not found.");
        }
    }
}
