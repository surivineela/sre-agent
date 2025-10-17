// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents
{
    public class ConnectorsDeploymentModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = "agent.platform.ai/v1";

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "ConnectorDeployment";

        [YamlMember(Alias = "metadata")]
        public YamlMetadata Metadata { get; set; } = new();

        [YamlMember(Alias = "spec")]
        public ConnectorSpec Spec { get; set; } = new();

        public class ConnectorSpec
        {
            [YamlMember(Alias = "connectors")]
            public List<ExtendedAgentConnectorApiModel> Connectors { get; set; } = new();
        }
    }

}
