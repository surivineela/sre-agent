using System.Text.Json.Serialization;

namespace Session.Proxy.Models;

/// <summary>
/// Response model for the bootstrap endpoint.
/// </summary>
public class BootstrapResponse
{
    /// <summary>
    /// Status message describing the outcome of the bootstrap operation.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    /// <summary>
    /// List of results for each bootstrap operation performed.
    /// </summary>
    [JsonPropertyName("results")]
    public List<BootstrapResultItem> Results { get; set; } = [];
}

/// <summary>
/// Represents the result of an individual bootstrap operation.
/// </summary>
public class BootstrapResultItem
{
    /// <summary>
    /// The type of operation performed (e.g., "managedIdentity", "tokens").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    /// <summary>
    /// Details of the managed identity operation result.
    /// </summary>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ManagedIdentityResultDetails? Details { get; set; }
}

/// <summary>
/// Details returned from the managed identity configuration operation.
/// </summary>
public class ManagedIdentityResultDetails
{
    /// <summary>
    /// Status message from the identity provider.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// The type of managed identity (e.g., "SystemAssigned", "UserAssigned").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// The client ID of the managed identity.
    /// </summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }

    /// <summary>
    /// The principal ID of the managed identity.
    /// </summary>
    [JsonPropertyName("principalId")]
    public string? PrincipalId { get; set; }
}
