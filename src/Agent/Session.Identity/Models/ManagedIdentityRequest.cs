using System.Text.Json.Serialization;

namespace Session.Identity.Models;

/// <summary>
/// Represents managed identity information.
/// </summary>
public class ManagedIdentityInfo
{
    /// <summary>
    /// The type of identity (SystemAssigned or UserAssigned).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// The bytes of the PFX certificate.
    /// </summary>
    [JsonPropertyName("pfxBytes")]
    public required byte[] PfxBytes { get; set; }

    /// <summary>
    /// The client ID of the managed identity.
    /// </summary>
    [JsonPropertyName("clientId")]
    public required string ClientId { get; set; }

    /// <summary>
    /// The principal ID of the managed identity.
    /// </summary>
    [JsonPropertyName("principalId")]
    public required string PrincipalId { get; set; }

    /// <summary>
    /// The tenant ID.
    /// </summary>
    [JsonPropertyName("tenantId")]
    public required string TenantId { get; set; }

    /// <summary>
    /// The authentication endpoint for the managed identity.
    /// </summary>
    [JsonPropertyName("authenticationEndpoint")]
    public required string AuthenticationEndpoint { get; set; }

    /// <summary>
    /// Indicates if this is the system-assigned identity.
    /// </summary>
    [JsonIgnore]
    public bool IsSystemAssigned => string.IsNullOrEmpty(Type) ||
        string.Equals(Type, "SystemAssigned", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Request model for adding a managed identity configuration via HTTP API.
/// Contains ManagedIdentityInfo with base64 certificate support for JSON transport.
/// </summary>
public class ManagedIdentityRequest
{
    /// <summary>
    /// The managed identity information.
    /// </summary>
    [JsonPropertyName("managedIdentity")]
    public ManagedIdentityInfo? ManagedIdentity { get; set; }
}
