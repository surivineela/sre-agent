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

        [Required]
        public TimerSettings Timer { get; set; } = new();

        [Required]
        public InstanceManagementSettings InstanceManagement { get; set; } = new();

        [Required]
        public KustoClusterConfiguration KustoClusterConfiguration { get; set; } = new();

        [Required]
        public bool UseAgentFramework { get; set; } = false;

        [Required]
        public bool EnableReasoningOutput { get; set; } = false;
    }
}
