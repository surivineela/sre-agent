// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Mocks
{
    public class MockGithubWorkflowTriggerPlugin : IGithubWorkflowTriggerPlugin
    {
        private readonly TimeProvider _timeProvider;
        private readonly List<WorkflowRunResponse> _workflowRuns = new();
        private long _nextRunId = 1;

        public IReadOnlyList<WorkflowRunResponse> WorkflowRuns => _workflowRuns;

        public MockGithubWorkflowTriggerPlugin(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public Task<PullRequestMergeStatus> CheckPullRequestMergeStatus(string repoUrl, int pullRequestNumber)
        {
            return Task.FromResult(new PullRequestMergeStatus(
                IsMerged: true,
                MergeCommitSha: "abc123",
                MergedAt: _timeProvider.GetUtcNow().DateTime,
                State: "closed",
                MergeableState: "clean"
            ));
        }

        public Task<WorkflowRunResponse> TriggerWorkflow(string repoUrl, string resourceId)
        {
            System.Diagnostics.Debug.WriteLine($"Triggering workflow for resource {resourceId}");
            
            var workflowIdentifier = "";
            if (resourceId.Contains("stage", StringComparison.OrdinalIgnoreCase))
            {
                workflowIdentifier = "main_oa-demo-web-stage.yml";
            }
            else if (resourceId.Contains("canary", StringComparison.OrdinalIgnoreCase))
            {
                workflowIdentifier = "main_oa-demo-web-canary.yml";
            }
            else
            {
                workflowIdentifier = "main_oa-demo-web-prod-westus.yml";
            }

            var response = new WorkflowRunResponse(
                RunId: _nextRunId++,
                Status: "queued",
                Conclusion: null,
                HtmlUrl: $"https://github.com/testorg/testrepo/actions/runs/{_nextRunId}",
                WorkflowIdentifier: workflowIdentifier
            );

            _workflowRuns.Add(response);
            System.Diagnostics.Debug.WriteLine($"Created workflow run {response.RunId} with identifier {workflowIdentifier}");
            System.Diagnostics.Debug.WriteLine($"Total workflow runs: {_workflowRuns.Count}");
            return Task.FromResult(response);
        }

        public Task<string> TrackWorkflow(string repoUrl, long runId)
        {
            System.Diagnostics.Debug.WriteLine($"Tracking workflow run {runId}");
            System.Diagnostics.Debug.WriteLine($"Available workflow runs: {string.Join(", ", _workflowRuns.Select(r => r.RunId))}");
            
            var run = _workflowRuns.Find(r => r.RunId == runId);
            if (run == null)
            {
                System.Diagnostics.Debug.WriteLine($"Workflow run {runId} not found in _workflowRuns list");
                throw new ArgumentException($"Workflow run {runId} not found");
            }

            // Complete the workflow immediately
            run.Status = "completed";
            run.Conclusion = "success";
            System.Diagnostics.Debug.WriteLine($"Successfully completed workflow run {runId}");
            return Task.FromResult("Workflow succeeded, I can move to monitoring the app health now");
        }
    }
}

