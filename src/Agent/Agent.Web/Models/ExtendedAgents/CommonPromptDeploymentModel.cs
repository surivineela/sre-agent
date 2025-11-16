// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents
{
    public class CommonPromptDeploymentModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = "azuresre.ai/v1";

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "CommonPromptDeployment";

        [YamlMember(Alias = "metadata")]
        public YamlMetadata Metadata { get; set; } = new();

        [YamlMember(Alias = "spec")]
        public CommonPromptSpec Spec { get; set; } = new();

        public class CommonPromptSpec
        {
            [YamlMember(Alias = "common_prompts")]
            public List<ExtendedAgentCommonPromptApiModel> CommonPrompts { get; set; } = new();
        }
    }

}
