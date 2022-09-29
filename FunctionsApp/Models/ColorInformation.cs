using Newtonsoft.Json;

namespace FunctionsApp.Models;

public class ColorInformation
{
    public class ColorName
    {
        [JsonProperty("Value")] public string Value { get; set; }
    }

    public ColorName Name { get; set; }
}
