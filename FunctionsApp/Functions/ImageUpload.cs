using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AssignmentFunction.ImageHelper;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FunctionsApp.Functions;

public class TableEntry : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Status;

    public TableEntry(string key, string status)
    {
        PartitionKey = "status";
        RowKey = key;
        Status = status;
    }

    public TableEntry()
    {
    }
}

public class ImageUpload
{
    // private readonly BlobServiceClient _blobServiceClient;
    // private readonly QueueServiceClient _queueServiceClient;
    private readonly QueueClient _queueClient;

    private readonly BlobContainerClient _blobContainerClient;

    private readonly MD5 _md5;
    private readonly TableClient _tableClient;
    private readonly HttpClient _httpClient;


    public ImageUpload(BlobServiceClient blobServiceClient, QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient, HttpClient httpClient)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient("container");
        _queueClient = queueServiceClient.GetQueueClient(Environment.GetEnvironmentVariable("ImageUploadQueueName"));

        _tableClient = tableServiceClient.GetTableClient("statusTable");

        _httpClient = httpClient;

        _md5 = MD5.Create();
    }


    /// <summary>
    /// This function is triggered via an HTTP POST request.
    /// If an image is passed via form data, it is uploaded to blob storage and a message is added to a queue.
    /// This will trigger the UploadQueueTrigger function.
    /// </summary>
    [FunctionName("ImageUpload")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequest req, ILogger log)


    {
        IFormFile f = req.Form.Files[0];
        if (f == null)
        {
            return new OkObjectResult("No file");
        }

        var myBlob = f.OpenReadStream();


        string ext;

        if (f.ContentType == "image/jpeg")
        {
            ext = ".jpg";
        }
        else if (f.ContentType == "image/png")
        {
            ext = ".png";
        }
        else
        {
            return new OkObjectResult("Invalid file type");
        }

        // var md5hash = new MD5CryptoServiceProvider().ComputeHash(myBlob);
        // // convert byte array to string
        // var stringmd5 = BitConverter.ToString(md5hash).Replace("-", "").ToLower();

        var stringmd5 = Helpers.GenerateMd5Hash(myBlob);

        BlobClient blob = _blobContainerClient.GetBlobClient(stringmd5 + ext);

        if (!await blob.ExistsAsync())
        {
            myBlob.Position = 0;
            await blob.UploadAsync(myBlob);
            // await _queueClient.SendMessageAsync(stringmd5 + ext);
            string encodedStr = Convert.ToBase64String(Encoding.UTF8.GetBytes(stringmd5 + ext));
            await _queueClient.SendMessageAsync(encodedStr);
        }
        else
        {
            Console.WriteLine("File already exists");
            // Todo: remove
            string encodedStr = Convert.ToBase64String(Encoding.UTF8.GetBytes(stringmd5 + ext));
            await _queueClient.SendMessageAsync(encodedStr);
        }

        var sasToken = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
        var sasTokenString = sasToken.AbsoluteUri;
        // generate sas uri of blob
        // blob.GenerateSasUri();

        var rnd = new Random();
        var rndId = rnd.Next(0, 999);

        // try
        // {
        //     var entry = await _tableClient.GetEntityAsync<TableEntity>("status", stringmd5);
        //     Console.WriteLine(entry);
        // }
        // catch (RequestFailedException e)
        // {
        //     Console.WriteLine(e);
        //     if (e.Status == 404)
        //         await _tableClient.AddEntityAsync(new TableEntry(stringmd5, "pending"));
        // }

        await DebugDelete(stringmd5, ext);
        return new OkObjectResult(stringmd5);

        // await queue.AddAsync("asd");
        // return new OkResult();
        // log.LogInformation("C# HTTP trigger function processed a request.");
        //
        // string name = req.Query["name"];
        //
        // var requestBody = await req.ReadAsStringAsync().ConfigureAwait(false);
        // dynamic data = JsonConvert.DeserializeObject(requestBody);
        // name = name ?? data?.name;
        //
        // // var queueMessage = new CloudQueueMessage(requestBody);
        // await sampleQueue.SendMessageAsync(requestBody).ConfigureAwait(false);
        //
        //
        // return name != null
        //     ? (ActionResult) new OkObjectResult($"Hello, {name}")
        //     : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
    }

    private async Task DebugDelete(string hash, string ext)
    {
        // await _blobContainerClient.DeleteBlobAsync(hash + ext);
        await _tableClient.DeleteEntityAsync("status", hash);
    }

    /// <summary>
    /// This function is called via queue trigger.
    /// It fetches the given image from blob storage and processes it.
    /// </summary>
    [FunctionName("UploadQueueTrigger")]
    public async Task RunAsync2([QueueTrigger("testq", Connection = "StorageAccountString")] string myQueueItem, ILogger log)
    {
        log.LogInformation($"UploadQueueTrigger");
        var image = _blobContainerClient.GetBlobClient(myQueueItem);


        var ms = new MemoryStream();
        await image.DownloadToAsync(ms);


        var colors = ImageHelper.EditImage(ms.ToArray());

        var res = await _httpClient.GetAsync($"https://www.thecolorapi.com/id?hex={colors.Item2?[0]}");

        // parse json response
        var json = await res.Content.ReadAsStringAsync();
        dynamic data = JsonConvert.DeserializeObject(json);
        var colorName = data.name.value;


        var quoteResponse = await _httpClient.GetAsync($"https://api.quotable.io/search/quotes?query={colorName}&fields=content,tags");

        var resJson = await quoteResponse.Content.ReadAsStringAsync();
        dynamic quoteData = JsonConvert.DeserializeObject(resJson);
        string quote = quoteData.results[0].content;
        // var outms = new MemoryStream();
        // await _blobContainerClient.GetBlobClient("converted").UploadAsync(new BinaryData(outp));

        BlobClient blob = _blobContainerClient.GetBlobClient("converted_" + myQueueItem);

        if (await blob.ExistsAsync())
        {
            await blob.DeleteAsync();
        }

        byte[] processedImage = ImageHelper.AddTextToImage(ms.ToArray(),
            (quote, (10, 10), 24, "ffffff")
        );

        await blob.UploadAsync(new BinaryData(processedImage));

        Console.WriteLine("qTrig");
        log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");
    }
}
