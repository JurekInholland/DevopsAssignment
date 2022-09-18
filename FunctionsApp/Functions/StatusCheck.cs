using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Data.Tables;
using FunctionsApp.Functions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FunctionsApp;

public class StatusRequest
{
    public string Name { get; set; }
}

public class StatusCheck
{
    private readonly TableClient _tableClient;

    public StatusCheck(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("statusTable");
    }

    [FunctionName("StatusCheck")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]
        HttpRequest req, ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        string name = req.Query["name"];

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        StatusRequest status = JsonConvert.DeserializeObject<StatusRequest>(requestBody);
        // name = name ?? data?.name;

        if (status?.Name != null)
        {
            var query = _tableClient.Query<TableEntry>($"PartitionKey eq '{status.Name}'");


            var test = _tableClient.GetEntity<TableEntity>("status", status.Name);

            test.Value.TryGetValue("Status",out object value);
            Console.WriteLine(status.Name);


            return new OkObjectResult(value);


        }


        return new OkObjectResult("No name was given.");

        // return name != null
        //     ? (ActionResult) new OkObjectResult($"Hello, {name}")
        //     : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
    }
}
