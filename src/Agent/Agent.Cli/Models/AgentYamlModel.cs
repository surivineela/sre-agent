// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML wrapper for agent configurations.
    /// Uses shared AgentSpec and ResourceMetadata from Agent.Data.DataModels.
    /// Adds YAML envelope fields (api_version, kind) for file serialization.
    /// </summary>
    public class AgentYamlModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = "azuresre.ai/v2";

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "ExtendedAgent";

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// Uses shared ResourceMetadata from Agent.Data.DataModels.
        /// </summary>
        [YamlMember(Alias = "metadata")]
        public ResourceMetadata Metadata { get; set; } = new();

        /// <summary>
        /// Agent specification properties.
        /// Uses shared AgentSpec from Agent.Data.DataModels.
        /// </summary>
        [YamlMember(Alias = "spec")]
        public AgentSpec Spec { get; set; } = new();
    }
}
