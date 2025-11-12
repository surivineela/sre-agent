using System.Text.Json.Serialization;

namespace Agent.Core.Helpers.ArmModels;

/**
 * This class is used to wrap the ARM resource properties.
 * All ARM resources have a common set of properties that are used to identify the resource.
 * and they differentiate on the properties that are specific to the resource type.
 * This abstract class is used to wrap those common properties.
 *
 * Add any new properties to this class.
 */
public class ArmWrapper<T>
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("properties")]
    public T? Properties { get; set; }
}
