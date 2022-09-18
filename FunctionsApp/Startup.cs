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
            string storageAccountString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            clientBuilder.AddBlobServiceClient(storageAccountString);
            clientBuilder.AddQueueServiceClient(storageAccountString);
            clientBuilder.AddTableServiceClient(storageAccountString);
            clientBuilder.UseCredential(new DefaultAzureCredential());

            // Set up any default settings
            // clientBuilder.ConfigureDefaults(builder.Configuration.GetSection("AzureDefaults"));
        });
        builder.Services.AddHttpClient();
    }
}
