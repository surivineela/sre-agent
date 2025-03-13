using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

public record PagedResponse<T>(
    [property: JsonPropertyName("value")] IEnumerable<T> Value
);
