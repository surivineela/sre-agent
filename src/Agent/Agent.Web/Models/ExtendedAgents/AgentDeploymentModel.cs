// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents
{
    public class AgentDeploymentModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = string.Empty;

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "AgentConfiguration";

        [YamlMember(Alias = "metadata")]
        public YamlMetadata Metadata { get; set; } = new();

       

        [YamlMember(Alias = "spec")]
        public AgentSpec Spec { get; set; } = new();

        public class AgentSpec
        {
            [YamlMember(Alias = "agent")]
            public ExtendedAgentApiModel Agent { get; set; } = new();
            [YamlMember(Alias = "tools")]
            public List<ExtendedAgentToolApiModel> Tools { get; set; } = new();

            [YamlMember(Alias = "connectors")]
            public List<ExtendedAgentConnectorApiModel> Connectors { get; set; } = new();
        }
    }
}
