using Newtonsoft.Json;

namespace FunctionsApp.Models;

public class ColorInformation
{
    [JsonProperty("name")]
    public string Name;
}
