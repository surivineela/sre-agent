// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace Agent.Core.Models.Streaming
{
    public class ChannelData
    {
        [JsonProperty(PropertyName = "streamId")]
        public string StreamId { get; set; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        [JsonProperty(PropertyName = "streamType")]
        public StreamType StreamType { get; set; } = StreamType.Streaming;

        [JsonProperty(PropertyName = "streamSequence")]
        public int StreamSequence { get; set; }
    }
}
