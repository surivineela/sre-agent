using System;
using System.Text.Json.Serialization;

namespace Session.Proxy.Models;

public class MsiResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("expires_on")]
    public required string ExpiresOn { get; set; }
    [JsonPropertyName("resource")]
    public required string Resource { get; set; }
    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }
    [JsonPropertyName("client_id")]
    public required string ClientId { get; set; }
}
