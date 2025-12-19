using System.Text.Json.Serialization;
using Session.Identity.Models;

namespace Session.Proxy.Models;

/// <summary>
/// Request model for bootstrapping the identity provider with managed identity and tokens.
/// </summary>
public class BootstrapRequest
{
    /// <summary>
    /// Managed identity information. Optional.
    /// </summary>
    [JsonPropertyName("managedIdentity")]
    public ManagedIdentityInfo? ManagedIdentity { get; set; }

    /// <summary>
    /// Dictionary of resource/scope to raw token string. Optional.
    /// </summary>
    [JsonPropertyName("tokens")]
    public Dictionary<string, string>? Tokens { get; set; }
}
