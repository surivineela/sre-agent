// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    public class AgentDeploymentModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = "azuresre.ai/v1";

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "AgentConfiguration";

        [YamlMember(Alias = "metadata")]
        public YamlMetadata Metadata { get; set; } = new();

        [YamlMember(Alias = "spec")]
        public YamlAgentDescriptor Spec { get; set; } = new();
    }
}
