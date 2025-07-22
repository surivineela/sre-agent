// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Models.Resources
{
    public class WebAppModel
    {
        public string ResourceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string SlotName { get; set; } = "Production";
    }

    public class StampSiteModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }    = string.Empty;

        [JsonProperty("web_workers")]
        public List<WebWorkerDetails> WebWorkers { get; set; } = [];

        [JsonProperty("hostnames")]
        public List<HostnameDetails> Hostnames { get; set; } = [];
    }

    public class WebWorkerDetails
    {
        [JsonProperty("instance_name")]
        public string InstanceName { get; set; } = string.Empty;

        [JsonProperty("reboot_link")]
        public string RebootLink { get; set; } = string.Empty;

        [JsonProperty("reimage_link")]
        public string ReimageLink { get; set; } = string.Empty;
    }

    public class HostnameDetails
    {
        [JsonProperty("hostname")]
        public string Hostname { get; set; } = string.Empty;
        [JsonProperty("link")]
        public string Link { get; set; } = string.Empty;
        [JsonProperty("hostname_type")]
        public int HostnameType { get; set; }
    }
}

