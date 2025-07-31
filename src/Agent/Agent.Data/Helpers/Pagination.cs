using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;



public class Pagination
{
    [Required]
    public int TotalCount { get; set; }

    [Required]
    public int Limit { get; set; }

    [Required]
    public int Offset { get; set; }

    [Required]
    public bool HasMore { get; set; }
}

public class PaginatedList<T> : List<T>
{
    [JsonPropertyName("page_index")]
    public int PageIndex { get; private set; }
    [JsonPropertyName("total_pages")]
    public int TotalPages { get; private set; }
    [JsonPropertyName("page_size")]
    public int PageSize { get; private set; }
    [JsonPropertyName("total_count")]
    public int TotalCount { get; private set; }
    [JsonPropertyName("has_previous_page")]
    public bool HasPreviousPage => PageIndex > 1;
    [JsonPropertyName("has_next_page")]
    public bool HasNextPage => PageIndex < TotalPages;

    public PaginatedList(IEnumerable<T> items, int count, int pageIndex, int pageSize)
    {
        TotalCount = count;
        PageSize = pageSize;
        PageIndex = pageIndex;
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);

        AddRange(items);
    }

    public static PaginatedList<T> Create(IEnumerable<T> source, int pageIndex, int pageSize)
    {
        var count = source.Count();
        var items = source.Skip((pageIndex - 1) * pageSize).Take(pageSize);
        return new PaginatedList<T>(items, count, pageIndex, pageSize);
    }
}

