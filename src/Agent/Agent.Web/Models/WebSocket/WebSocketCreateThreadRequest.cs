// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Core.Models.Api.v1;

namespace Agent.Web.Models.WebSocket
{
    public class WebSocketCreateThreadRequest
    {
        [JsonRequired]
        [JsonPropertyName("startMessage")]
        public WebSocketCreateMessageRequest StartMessage { get; set; }

        [JsonPropertyName("source"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ThreadSource? Source { get; set; } = null;
    }
}
