// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Runtime.Models.ExtendedAgents;

namespace Agent.Web.Models.ExtendedAgents.Response;

public class PaginatedResponse<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; }= new List<T>();

    [JsonPropertyName("page_index")]
    public int PageIndex { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("has_previous_page")]
    public bool HasPreviousPage { get; set; }

    [JsonPropertyName("has_next_page")]
    public bool HasNextPage { get; set; }

    public static PaginatedResponse<T> FromPaginatedList(PaginatedList<T> list)
    {
        return new PaginatedResponse<T>
        {
            Data = list.ToList(),
            PageIndex = list.PageIndex,
            TotalPages = list.TotalPages,
            PageSize = list.PageSize,
            TotalCount = list.TotalCount,
            HasPreviousPage = list.HasPreviousPage,
            HasNextPage = list.HasNextPage
        };
    }
}
