using System.Text.Json.Serialization;

namespace Agent.Core.Models;

public class AgentSpaceTokenResponse
{
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; set; }

    [JsonPropertyName("expiresOn")]
    public required DateTime ExpiresOn { get; set; }
}
