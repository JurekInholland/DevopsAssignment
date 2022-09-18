using System.Collections.Generic;
using Newtonsoft.Json;

namespace FunctionsApp.Models;

public class WikiResult
{
    public class Query
    {
        public class Page
        {
            [JsonProperty("extract")] public string Extract { get; set; }
        }

        [JsonProperty("pages")] public Dictionary<int, Page> Pages { get; set; }
    }

    public Query query;
}
