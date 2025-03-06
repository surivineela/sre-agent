using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Core.Models;

namespace Agent.Plugins
{
    public class GithubWorkflowTriggerPluginDefinition
    {
        private readonly IGithubWorkflowTriggerPlugin _githubWorkflowTriggerPlugin;

        public GithubWorkflowTriggerPluginDefinition(IGithubWorkflowTriggerPlugin githubWorkflowTriggerPlugin)
        {
            _githubWorkflowTriggerPlugin = githubWorkflowTriggerPlugin;
        }

        [Description("Check if a specific pull request has been merged and return its current merge status")]
        public async Task<PullRequestMergeStatus> CheckPullRequestMergeStatus(
            [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
            [Description("The pull request number to check. Just the number eg: 123 (no #)")] int pullRequestNumber)
        {
            return await _githubWorkflowTriggerPlugin.CheckPullRequestMergeStatus(repoUrl, pullRequestNumber);
        }

        [Description("Manually trigger a GitHub Actions workflow. Used for triggering Canary and Prod workflows. Workflow name can be found by calling detect_github_workflow_name")]
        public async Task<WorkflowRunResponse> TriggerWorkflow(
            [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
            [Description("App Azure ResourceId")] string resourceId)
        {
            return await _githubWorkflowTriggerPlugin.TriggerWorkflow(repoUrl, resourceId);
        }

        [Description("Check the status of a dispatched GitHub Actions workflow run and return a status message.")]
        public async Task<string> TrackWorkflow(
            [Description("GitHub repository URL, e.g. https://github.com/owner/repo-name")] string repoUrl,
            [Description("The run ID of the workflow run to track")] long runId)
        {
            return await _githubWorkflowTriggerPlugin.TrackWorkflow(repoUrl, runId);
        }
    }
}
