// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents
{
    public class CommonToolsListDeploymentModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = "agent.platform.ai/v1";

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "CommonToolsDeployment";

        [YamlMember(Alias = "metadata")]
        public YamlMetadata Metadata { get; set; } = new();

        [YamlMember(Alias = "spec")]
        public CommonToolsListSpec Spec { get; set; } = new();

        public class CommonToolsListSpec
        {
            [YamlMember(Alias = "common_tools_lists")]
            public List<ExtendedAgentCommonToolsListApiModel> CommonToolsLists { get; set; } = new();
        }
    }

}
