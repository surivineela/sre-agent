using System.Text.Json.Serialization;

namespace Session.Identity.Models;

/// <summary>
/// Request model for adding tokens to the identity provider.
/// </summary>
public class AddTokensRequest
{
    /// <summary>
    /// Dictionary of resource/scope to raw token string.
    /// </summary>
    [JsonPropertyName("tokens")]
    public Dictionary<string, string>? Tokens { get; set; }
}
