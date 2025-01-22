using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Models
{
    public class OpenAIMessage
    {
        [JsonProperty("role")]
        public string? Role { get; set; }

        [JsonProperty("content")]
        public List<OpenAIMessageContent>? Content { get; set; } 
    }

    public class OpenAIMessageContent
    {
        [JsonProperty("type")]
        public string? Type { get;set; }

        [JsonProperty("text")]
        public string? Text { get; set; }
    }
}
