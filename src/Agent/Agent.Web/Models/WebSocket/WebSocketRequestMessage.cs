// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Web.Models.WebSocket
{
    public class WebSocketRequestMessage
    {
        [JsonPropertyName("threadId")]
        public string? ThreadId { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("messageType")]
        public string? MessageType { get; set; }

        // String JSON representation of object type as defined from MessageType
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; set; }

        // Only used for debugging and testing purposes
        [JsonPropertyName("textOnly"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? TextOnly { get; set; }

        // Mirrored back for all requests associated with a specific stream
        [JsonPropertyName("streamId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StreamId { get; set; }
    }
}
