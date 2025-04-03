// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Models.Resources
{
    public class WebAppModel
    {
        public string ResourceId { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string SlotName { get; set; } = "Production";
    }

    public class StampSiteModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("web_workers")]
        public List<WebWorkerDetails> WebWorkers { get; set; }

        [JsonProperty("hostnames")]
        public List<HostnameDetails> Hostnames { get; set; }
    }

    public class WebWorkerDetails
    {
        [JsonProperty("instance_name")]
        public string InstanceName { get; set; }

        [JsonProperty("reboot_link")]
        public string RebootLink { get; set; }

        [JsonProperty("reimage_link")]
        public string ReimageLink { get; set; }
    }

    public class HostnameDetails
    {
        [JsonProperty("hostname")]
        public string Hostname { get; set; }
        [JsonProperty("link")]
        public string Link { get; set; }
        [JsonProperty("hostname_type")]
        public int HostnameType { get; set; }
    }
}

