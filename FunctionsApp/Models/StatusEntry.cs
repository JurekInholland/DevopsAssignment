using System;
using Azure;
using Azure.Data.Tables;

namespace FunctionsApp.Models;

public class StatusEntry : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Status;

    public StatusEntry(string key, string status)
    {
        PartitionKey = "status";
        RowKey = key;
        Status = status;
    }

    public StatusEntry()
    {
    }
}
