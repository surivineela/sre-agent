using System.Text.Json.Serialization;

namespace Agent.Web.Models.v1;

public record PagedResponse<T>(
    [property: JsonPropertyName("value")] IEnumerable<T> Value
);
