// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Framework.Skills;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents;

public class SkillDeploymentModel
{
    [YamlMember(Alias = "api_version")]
    public string ApiVersion { get; set; } = "agent.platform.ai/v1";

    [YamlMember(Alias = "kind")]
    public string Kind { get; set; } = "Skill";

    [YamlMember(Alias = "metadata")]
    public YamlMetadata Metadata { get; set; } = new();

    [YamlMember(Alias = "spec")]
    public SkillSpec Spec { get; set; } = new();
}
