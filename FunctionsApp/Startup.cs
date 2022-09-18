using System;
using Azure.Identity;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;

using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(FunctionsApp.Startup))]

namespace FunctionsApp;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddAzureClients(clientBuilder =>
        {
            // var connString =
            //     "DefaultEndpointsProtocol=https;AccountName=jurekgrpb8f9;AccountKey=zrEtSNAp63vTom5AsSNC3wM7F9WP3MBQislvyqIQe83m/2++mW6eZjiZ/XCnmWWjbey5dCrBY2xC+AStLMfa/A==;EndpointSuffix=core.windows.net";
            // Add a storage account client
            clientBuilder.AddBlobServiceClient(Environment.GetEnvironmentVariable("StorageAccountString"));
            clientBuilder.AddQueueServiceClient(Environment.GetEnvironmentVariable("StorageAccountString"));
            clientBuilder.AddTableServiceClient(Environment.GetEnvironmentVariable("StorageAccountString"));
            // Use DefaultAzureCredential by default
            clientBuilder.UseCredential(new DefaultAzureCredential());

            // Set up any default settings
            // clientBuilder.ConfigureDefaults(builder.Configuration.GetSection("AzureDefaults"));
        });
        builder.Services.AddHttpClient();
    }
}
