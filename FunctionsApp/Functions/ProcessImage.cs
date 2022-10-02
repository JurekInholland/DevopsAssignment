using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AssignmentFunction.ImageHelper;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FunctionsApp.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FunctionsApp.Functions;

public class ProcessImage
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly TableClient _tableClient;
    private readonly HttpClient _httpClient;

    public ProcessImage(BlobServiceClient blobServiceClient, TableServiceClient tableServiceClient, HttpClient httpClient)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BlobContainerName"));
        _tableClient = tableServiceClient.GetTableClient(Environment.GetEnvironmentVariable("TableName"));
        _httpClient = httpClient;
    }

    /// <summary>
    /// This function is called via queue trigger.
    /// It fetches the given image from blob storage and processes it.
    /// </summary>
    [FunctionName("processImage")]
    public async Task RunAsync([QueueTrigger("image-queue", Connection = "AzureWebJobsStorage")] string queueMessage, ILogger log)
    {
        QueueMessage message = JsonConvert.DeserializeObject<QueueMessage>(queueMessage);

        string id = message?.Id;

        log.LogInformation("UploadQueueTrigger");

        BlobClient image = new BlobClient(AssembleSasTokenUri(message));
        MemoryStream ms = new();


        await UpdateStatus(id, "downloading image");
        log.LogInformation("Downloading image via SasToken...");
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
        ColorInformation colorInfo = JsonConvert.DeserializeObject<ColorInformation>(content);

        return colorInfo?.Name is null ? "Unknown color" : colorInfo.Name.Value;
    }

    private Uri AssembleSasTokenUri(QueueMessage message)
    {
        return new Uri(_blobContainerClient.Uri + "/" + message.Id + message.Extension + message.SasQuery);
    }
}
