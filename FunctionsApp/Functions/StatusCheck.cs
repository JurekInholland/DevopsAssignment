using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using FunctionsApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace FunctionsApp.Functions;

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
        if (!req.Query.TryGetValue("id", out StringValues id))
        {
            return new BadRequestObjectResult("Missing id");
        }

        try
        {
            Response<StatusEntry> response = await _tableClient.GetEntityAsync<StatusEntry>("status", id);
            return new OkObjectResult("status: " + response.Value.Status);
        }
        catch (RequestFailedException)
        {
            return new BadRequestObjectResult("The given resource was not found.");
        }
    }
}
