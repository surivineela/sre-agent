// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Agent.Web.Models.Streaming
{
    public class ChannelData
    {
        [JsonProperty(PropertyName = "streamId")]
        public required string StreamId { get; set; }

        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        [JsonProperty(PropertyName = "streamType")]
        public required StreamType StreamType { get; set; }

        [JsonProperty(PropertyName = "streamSequence")]
        public required int StreamSequence { get; set; }
    }
}
