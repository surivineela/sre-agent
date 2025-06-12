// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Plugins.Interface
{
    public interface IGithubWorkflowTriggerPlugin
    {
        Task<PullRequestMergeStatus> CheckPullRequestMergeStatus(string repoUrl, int pullRequestNumber);
        Task<WorkflowRunResponse> TriggerWorkflow(string repoUrl, string resourceId);
        Task<string> TrackWorkflow(string repoUrl, long runId);
    }
}

