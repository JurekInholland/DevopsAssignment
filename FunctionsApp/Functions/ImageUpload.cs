using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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

namespace FunctionsApp.Functions;

public class ImageUpload
{
    private readonly QueueClient _queueClient;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly TableClient _tableClient;

    public ImageUpload(BlobServiceClient blobServiceClient, QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BlobContainerName"));
        _queueClient = queueServiceClient.GetQueueClient(Environment.GetEnvironmentVariable("ImageUploadQueueName"));
        _tableClient = tableServiceClient.GetTableClient(Environment.GetEnvironmentVariable("TableName"));
    }


    /// <summary>
    /// This function is triggered via a HTTP POST request.
    /// If an image is passed via form data, it is uploaded to blob storage and a message is added to a queue.
    /// This will trigger the processImage function.
    /// </summary>
    [FunctionName("upload")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequest req, ILogger log)
    {
        if (req.Form.Files.Count == 0)
        {
            return new BadRequestObjectResult("No file provided.");
        }
        IFormFile f = req.Form.Files[0];
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

        // Todo: use
        Uri sasToken = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
        string sasTokenString = sasToken.AbsoluteUri;

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
            $"Your file is being processed.\nid: {md5Hash}\nCheck the status: /api/status?id={md5Hash}\nView the result: /api/results?id={md5Hash}");
    }



    private async Task CreateQueueMessage(string message)
    {
        string base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
        await _queueClient.SendMessageAsync(base64Message);
    }
}
