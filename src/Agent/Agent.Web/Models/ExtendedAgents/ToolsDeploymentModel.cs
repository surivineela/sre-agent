// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents
{
    public class ToolsDeploymentModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = "agent.platform.ai/v1";

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "ToolList";

        [YamlMember(Alias = "metadata")]
        public YamlMetadata Metadata { get; set; } = new();

        [YamlMember(Alias = "spec")]
        public ToolSpec Spec { get; set; } = new();

        public class ToolSpec
        {
            [YamlMember(Alias = "tools")]
            public List<ExtendedAgentToolApiModel> Tools { get; set; } = new();
        }
    }
}
