using Newtonsoft.Json;

namespace FunctionsApp.Models;

public class QueueMessage
{
    [JsonRequired] public string Id { get; set; }
    [JsonRequired] public string Extension { get; set; }
    [JsonRequired] public string SasQuery { get; set; }
}
