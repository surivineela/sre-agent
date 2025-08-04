// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class CoreSettings
    {
        [Required]
        public AzureSettings Azure { get; set; } = new();

        [Required]
        public ExternalSettings External { get; set; } = new();

        public ExperimentalSettings Experimental { get; set; } = new();

        [Required]
        public TimerSettings Timer { get; set; } = new();

        [Required]
        public InstanceManagementSettings InstanceManagement { get; set; } = new();

        [Required]
        public KustoClusterConfiguration KustoClusterConfiguration { get; set; } = new();

        public DataConnectorSettings DataConnectorSettings { get; init; } = new();

        /// <summary>
        /// Instances of data connectors to be used by the agent. These are provided by the user and can be configured to connect to different data sources.
        /// </summary>
        public List<DataConnectorInstanceSettings> DataConnectors { get; init; } = [];

        [Required]
        public bool UseAgentFramework { get; set; } = false;

        // set to true to enable agent tasks (incident investigation, etc.)
        // Also controls whether to use test_meta_agent (when true) or meta_agent (when false) as default
        public bool AgentTasksEnabled { get; set; } = false;

        [Required]
        public bool EnableReasoningOutput { get; set; } = false;

        [Required]
        public AgentMemorySettings AgentMemory { get; set; } = new();
    }
}
