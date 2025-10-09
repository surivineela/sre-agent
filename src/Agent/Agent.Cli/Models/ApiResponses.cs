// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Cli.Models
{
    // Mirrors Agent.Web.Models.ExtendedAgents.Response.PaginatedResponse<T>
    public class PaginatedResponse<T>
    {
        [JsonPropertyName("data")] public List<T>? Data { get; set; }
        [JsonPropertyName("page_index")] public int PageIndex { get; set; }
        [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
        [JsonPropertyName("page_size")] public int PageSize { get; set; }
        [JsonPropertyName("total_count")] public int TotalCount { get; set; }
        [JsonPropertyName("has_previous_page")] public bool HasPreviousPage { get; set; }
        [JsonPropertyName("has_next_page")] public bool HasNextPage { get; set; }
    }

    // Minimal projection of agent/tool items we need in CLI
    public class AgentListItem
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        // Keep optional properties in case we show more later
        [JsonPropertyName("system_prompt")] public string? SystemPrompt { get; set; }
        [JsonPropertyName("instructions")] public string? Instructions { get; set; }
        [JsonPropertyName("handoffDescription")] public string? HandoffDescription { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("tools")] public List<string>? Tools { get; set; }
        [JsonPropertyName("handoffs")] public List<string>? Handoffs { get; set; }
    }

    public class ToolListItem
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
    }
}
