// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Octokit;
using Agent.Core.Models;
using Agent.Logging;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Implementation
{
    public class GithubWorkflowTriggerPlugin : IGithubWorkflowTriggerPlugin
    {
        private readonly ILogger<GithubWorkflowTriggerPlugin> _logger;
        private readonly IGitHubClient _gitHubClient;

        public GithubWorkflowTriggerPlugin(ILogger<GithubWorkflowTriggerPlugin> logger, IGitHubClient gitHubClient)
        {
            _logger = logger;
            _gitHubClient = gitHubClient;
        }

        public async Task<PullRequestMergeStatus> CheckPullRequestMergeStatus(string repoUrl, int pullRequestNumber)
        {
            try
            {
                var (owner, repo) = ParseGitHubUrl(repoUrl);
                var pullRequest = await _gitHubClient.PullRequest.Get(owner, repo, pullRequestNumber);

                return new PullRequestMergeStatus(
                    IsMerged: pullRequest.Merged,
                    MergeCommitSha: pullRequest.MergeCommitSha,
                    MergedAt: pullRequest.MergedAt,
                    State: pullRequest.State.StringValue,
                    MergeableState: pullRequest.MergeableState.Value.ToString()
                );
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error checking pull request merge status for PR {PullRequestNumber} in {RepoUrl}", pullRequestNumber, repoUrl);
                throw;
            }
        }

        public async Task<WorkflowRunResponse> TriggerWorkflow(string repoUrl, string resourceId)
        {
            try
            {
                var (owner, repo) = ParseGitHubUrl(repoUrl);

                var createWorkflowDispatch = new CreateWorkflowDispatch("main")
                {
                    Inputs = new Dictionary<string, object>()
                };
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
                await _gitHubClient.Actions.Workflows.CreateDispatch(owner, repo, workflowIdentifier, createWorkflowDispatch);

                Thread.Sleep(2000);

                // Get the latest run for this workflow
                var workflowRuns = await _gitHubClient.Actions.Workflows.Runs.List(owner, repo);

                var latestRun = workflowRuns.WorkflowRuns.FirstOrDefault();

                return new WorkflowRunResponse(
                    RunId: latestRun?.Id ?? 0,
                    Status: latestRun?.Status.StringValue ?? "Unknown",
                    Conclusion: latestRun?.Conclusion?.ToString() ?? "Unknown",
                    HtmlUrl: latestRun?.HtmlUrl ?? string.Empty,
                    WorkflowIdentifier: workflowIdentifier
                );
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error triggering workflow for resource {ResourceId} in {RepoUrl}", resourceId, repoUrl);
                throw;
            }
        }

        public async Task<string> TrackWorkflow(string repoUrl, long runId)
        {
            try
            {
                // Extract the owner and repository name from the provided URL.
                var (owner, repo) = ParseGitHubUrl(repoUrl);

                // Retrieve the workflow run details from GitHub.
                var workflowRun = await _gitHubClient.Actions.Workflows.Runs.Get(owner, repo, runId);

                // Check if the workflow run is still in progress.
                if (workflowRun.Status.StringValue.Equals("waiting", StringComparison.OrdinalIgnoreCase))
                {
                    return "Workflow waiting it's turn, I would track again in 1 minute";
                }
                else if (!workflowRun.Status.StringValue.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    return "Still running, I would track again in 30 seconds";
                }
                else
                {
                    // Here we assume that a completed run means the workflow succeeded.
                    // If you need to handle non-successful completions, consider checking workflowRun.Conclusion.
                    return "Workflow succeeded, I can move to monitoring the app health now";
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error tracking workflow run {RunId} in {RepoUrl}", runId, repoUrl);
                throw;
            }
        }

        private static (string owner, string repo) ParseGitHubUrl(string repoUrl)
        {
            var match = Regex.Match(repoUrl, @"github\.com/([^/]+)/([^/]+)");
            if (!match.Success)
            {
                throw new ArgumentException($"Invalid GitHub URL format: {repoUrl}");
            }
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        private string ExtractResourceName(string resourceId)
        {
            // Extract the resource name from Azure resource ID
            var match = Regex.Match(resourceId, @"/([^/]+)$");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}

