using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;

namespace Agent.Core.Models;
public class SearchArticle
{
    [SimpleField(IsKey = true)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [SearchableField]
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [SearchableField]
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [SimpleField]
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [SearchableField]
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }
}

