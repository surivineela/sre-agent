// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents
{
    public class PluginConfigDeploymentModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = "agent.platform.ai/v1";

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "PluginConfigDeployment";

        [YamlMember(Alias = "metadata")]
        public YamlMetadata Metadata { get; set; } = new();

        [YamlMember(Alias = "spec")]
        public PluginConfigSpec Spec { get; set; } = new();

        public class PluginConfigSpec
        {
            [YamlMember(Alias = "plugin_name")]
            public string PluginName { get; set; } = string.Empty;

            [YamlMember(Alias = "config")]
            public Dictionary<string, object> Config { get; set; } = new();
        }
    }

}
