// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
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
        public YamlAgentDescriptor Spec { get; set; } = new();
    }
}
