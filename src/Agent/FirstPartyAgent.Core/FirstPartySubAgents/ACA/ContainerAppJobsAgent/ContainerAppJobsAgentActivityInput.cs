// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common; // Added for BaseContainerAppIssueActivityInput

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppJobsAgent // Ensured namespace is correct
{
    /// <summary>
    /// Input for the JobsAgent's initial activity.
    /// Inherits from BaseContainerAppIssueActivityInput similar to ContainerAppRevisionAgentActivityInput.
    /// </summary>
    public record ContainerAppJobsAgentActivityInput : BaseContainerAppIssueActivityInput
    {
        [Description("[Required] The name of the Container App Job.")]
        public string JobName { get; init; } = string.Empty;

        [Description("The specific execution ID of the Container App Job, if applicable.")]
        public string JobExecutionId { get; init; } = string.Empty;

        [Description("[Required] The name of the managed kubernetes cluster backing the managed environment")]
        public string ManagedClusterName { get; init; } = string.Empty;

        // Add any other properties specific to diagnosing jobs,
        // mirroring how ContainerAppRevisionAgentActivityInput has properties for revisions.
        // For example, if a cluster name or resource group is always needed directly in the activity input
        // beyond what BaseContainerAppIssueActivityInput provides, it could be added here.
    }
}
