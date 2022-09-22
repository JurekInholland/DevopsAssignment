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
    /// This function is triggered via a HTTP POST request.
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
            return new BadRequestObjectResult("No file was provided.");
        }

        Stream myBlob = f.OpenReadStream();


        BlobHttpHeaders header = new()
        {
            ContentType = f.ContentType
        };

        string ext;

        switch (f.ContentType)
        {
            case "image/jpeg":
                ext = ".jpg";
                break;
            case "image/png":
                ext = ".png";
                break;
            default:
                return new BadRequestObjectResult("Invalid file type. Only .jpg and .png files are supported.");
        }


        string md5Hash = Helpers.GenerateMd5Hash(myBlob);

        BlobClient blob = _blobContainerClient.GetBlobClient(md5Hash + ext);

        if (!await blob.ExistsAsync())
        {
            myBlob.Position = 0;
            await blob.UploadAsync(myBlob, header);
            await CreateQueueMessage(md5Hash + ext);
        }
        else
        {
            return new OkObjectResult($"Your file has already been processed!\nresult: /api/results?id={md5Hash}");
        }

        Uri sasToken = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
        string sasTokenString = sasToken.AbsoluteUri;
        // generate sas uri of blob
        // blob.GenerateSasUri();


        try
        {
            Response<TableEntity> entry = await _tableClient.GetEntityAsync<TableEntity>("status", md5Hash);
            Console.WriteLine(entry);
        }
        catch (RequestFailedException e)
        {
            if (e.Status == 404)
                await _tableClient.AddEntityAsync(new StatusEntry(md5Hash, "pending"));
        }

        return new OkObjectResult(
            $"Your file is being processed.\nid: {md5Hash}\nurl: {sasTokenString}\nstatus: /api/status?id={md5Hash}\nresult: /api/results?id={md5Hash}");
    }


    /// <summary>
    /// This function is called via queue trigger.
    /// It fetches the given image from blob storage and processes it.
    /// </summary>
    [FunctionName("UploadQueueTrigger")]
    public async Task RunAsync2([QueueTrigger("image-queue", Connection = "AzureWebJobsStorage")] string fileName, ILogger log)
    {
        string id = fileName.Split('.')[0];

        log.LogInformation($"UploadQueueTrigger");
        BlobClient image = _blobContainerClient.GetBlobClient(fileName);
        MemoryStream ms = new();


        await UpdateStatus(id, "downloading image");
        log.LogInformation("Downloading image...");
        await image.DownloadToAsync(ms);


        await UpdateStatus(id, "extracting colors");
        log.LogInformation("Extracting primary colors...");
        (byte[], string[]) colors = ImageHelper.EditImage(ms.ToArray(), 2);


        await UpdateStatus(id, "fetching color name");
        log.LogInformation("Fetching color name...");
        string colorName = await GetColorName(colors.Item2[0]);


        await UpdateStatus(id, "fetching wiki extract");
        string wikiExtract = await FetchWikiExtract(colorName);


        BlobClient blob = _blobContainerClient.GetBlobClient("converted_" + id + ".png");

        if (await blob.ExistsAsync())
        {
            await blob.DeleteAsync();
        }


        await UpdateStatus(id, "adding text");

        log.LogInformation("Adding text to image...");
        byte[] processedImage = ImageHelper.AddTextToImage(ms.ToArray(),
            (colorName, (10, 10), 48, "ffffff"),
            (wikiExtract, (10, 80), 24, "ffffff")
        );

        await UpdateStatus(id, "uploading image");

        log.LogInformation("Uploading processed image...");
        BlobHttpHeaders header = new()
        {
            ContentType = "image/png"
        };

        await blob.UploadAsync(new MemoryStream(processedImage), header);

        await UpdateStatus(id, "done");

        log.LogInformation($"C# Queue trigger function processed: {fileName}");
    }

    /// <summary>
    /// Returns the first sentence of the wikipedia page for the given query.
    /// </summary>
    private async Task<string> FetchWikiExtract(string query)
    {
        query = query.Split(" ")[0];

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"https://en.wikipedia.org/w/api.php?action=query&prop=extracts&exlimit=1&titles={query}&explaintext=1&exsectionformat=plain&format=json");
        string content = await response.Content.ReadAsStringAsync();
        WikiResult wikiResult = JsonConvert.DeserializeObject<WikiResult>(content);

        string extract = wikiResult?.query.Pages.Values.First().Extract;

        if (extract is null or "")
            return $"No wikipedia page found for query \"{query}\".";

        return extract.Split(". ")[0] + ".";
    }

    private async Task UpdateStatus(string id, string newStatus)
    {
        StatusEntry entity = await _tableClient.GetEntityAsync<StatusEntry>("status", id);
        entity.Status = newStatus;
        await _tableClient.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace);
    }

    private async Task<string> GetColorName(string hexColor)
    {
        HttpResponseMessage res = await _httpClient.GetAsync($"https://www.thecolorapi.com/id?hex={hexColor}");

        string content = await res.Content.ReadAsStringAsync();
        dynamic json = JsonConvert.DeserializeObject(content);
        return json?.name.value;
    }

    private async Task CreateQueueMessage(string message)
    {
        string base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
        await _queueClient.SendMessageAsync(base64Message);
    }
}
