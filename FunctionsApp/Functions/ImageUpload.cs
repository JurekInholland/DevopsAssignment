using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AssignmentFunction.ImageHelper;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using FunctionsApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FunctionsApp.Functions;



public class ImageUpload
{
    private readonly QueueClient _queueClient;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly TableClient _tableClient;
    private readonly HttpClient _httpClient;

    public ImageUpload(BlobServiceClient blobServiceClient, QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient, HttpClient httpClient)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BlobContainerName"));
        _queueClient = queueServiceClient.GetQueueClient(Environment.GetEnvironmentVariable("ImageUploadQueueName"));

        _tableClient = tableServiceClient.GetTableClient(Environment.GetEnvironmentVariable("TableName"));

        _httpClient = httpClient;
    }


    /// <summary>
    /// This function is triggered via an HTTP POST request.
    /// If an image is passed via form data, it is uploaded to blob storage and a message is added to a queue.
    /// This will trigger the UploadQueueTrigger function.
    /// </summary>
    [FunctionName("upload")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "")]
        HttpRequest req, ILogger log)


    {
        IFormFile f = req.Form.Files[0];
        if (f == null)
        {
            return new OkObjectResult("No file");
        }

        var myBlob = f.OpenReadStream();


        string ext;

        var header = new BlobHttpHeaders();
        header.ContentType = f.ContentType;


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
            return new OkObjectResult("Invalid file type. Please upload a .jpg or .png file.");
        }


        string md5Hash = Helpers.GenerateMd5Hash(myBlob);

        BlobClient blob = _blobContainerClient.GetBlobClient(md5Hash + ext);

        if (!await blob.ExistsAsync())
        {
            myBlob.Position = 0;
            await blob.UploadAsync(myBlob, header);
            string encodedStr = Convert.ToBase64String(Encoding.UTF8.GetBytes(md5Hash + ext));
            await _queueClient.SendMessageAsync(encodedStr);
        }
        else
        {
            return new OkObjectResult($"Your file has already been processed!\nresult: /api/Results?id=converted_{md5Hash + ext}");
        }

        var sasToken = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
        var sasTokenString = sasToken.AbsoluteUri;
        // generate sas uri of blob
        // blob.GenerateSasUri();


        try
        {
            Response<TableEntity> entry = await _tableClient.GetEntityAsync<TableEntity>("status", "converted_" + md5Hash + ext);
            Console.WriteLine(entry);
        }
        catch (RequestFailedException e)
        {
            if (e.Status == 404)
                await _tableClient.AddEntityAsync(new StatusEntry("converted_" + md5Hash + ext, "pending"));
        }

        return new OkObjectResult(
            $"Your file is being processed.\nid: {md5Hash}\nurl: {sasTokenString}\ntest: /api/Results?id=converted_{md5Hash}{ext}");
    }


    /// <summary>
    /// This function is called via queue trigger.
    /// It fetches the given image from blob storage and processes it.
    /// </summary>
    [FunctionName("UploadQueueTrigger")]
    public async Task RunAsync2([QueueTrigger("image-queue", Connection = "StorageAccountString")] string myQueueItem, ILogger log)
    {
        log.LogInformation($"UploadQueueTrigger");
        var image = _blobContainerClient.GetBlobClient(myQueueItem);
        var ms = new MemoryStream();


        await UpdateStatus(myQueueItem, "downloading image");
        log.LogInformation("Downloading image...");
        await image.DownloadToAsync(ms);

        var header = new BlobHttpHeaders
        {
            ContentType = (await image.GetPropertiesAsync()).Value.ContentType
        };

        await UpdateStatus(myQueueItem, "extracting colors");

        log.LogInformation("Extracting primary colors...");
        var colors = ImageHelper.EditImage(ms.ToArray(), 2);


        await UpdateStatus(myQueueItem, "fetching color name");

        log.LogInformation("Fetching color name...");

        string colorName = await GetColorName(colors.Item2[0]);


        await UpdateStatus(myQueueItem, "fetching wiki extract");

        log.LogInformation($"Fetching wiki extract for {colorName}...");
        string wikiExtract = await FetchWikiExtract(colorName);


        BlobClient blob = _blobContainerClient.GetBlobClient("converted_" + myQueueItem);

        if (await blob.ExistsAsync())
        {
            await blob.DeleteAsync();
        }


        await UpdateStatus(myQueueItem, "adding text");

        log.LogInformation("Adding text to image...");
        byte[] processedImage = ImageHelper.AddTextToImage(ms.ToArray(),
            (colorName, (10, 10), 48, "ffffff"),
            (wikiExtract, (10, 80), 24, "ffffff")
        );

        await UpdateStatus(myQueueItem, "uploading image");

        log.LogInformation("Uploading processed image...");
        await blob.UploadAsync(new MemoryStream(processedImage), header);

        await UpdateStatus(myQueueItem, "done");

        log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");
    }

    /// <summary>
    /// Returns the first paragraph of the wikipedia article for the given query.
    /// </summary>
    private async Task<string> FetchWikiExtract(string query)
    {
        query = query.Split(" ")[0];

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"https://en.wikipedia.org/w/api.php?action=query&prop=extracts&exlimit=1&titles={query}&explaintext=1&exsectionformat=plain&format=json");
        string content = await response.Content.ReadAsStringAsync();
        WikiResult wikiResult = JsonConvert.DeserializeObject<WikiResult>(content);

        if (wikiResult.query.Pages.Count == 0)
            return $"No wikipedia article found for '{query}'.";

        return wikiResult.query.Pages.Values.First().Extract.Split(". ")[0] + ".";
    }

    private async Task UpdateStatus(string id, string newStatus)
    {
        StatusEntry entity = await _tableClient.GetEntityAsync<StatusEntry>("status", "converted_" + id);
        entity.Status = newStatus;
        await _tableClient.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace);
    }

    private async Task<string> GetColorName(string hexColor)
    {
        var res = await _httpClient.GetAsync($"https://www.thecolorapi.com/id?hex={hexColor}");

        var content = await res.Content.ReadAsStringAsync();
        dynamic json = JsonConvert.DeserializeObject(content);
        return json.name.value;
    }
}
