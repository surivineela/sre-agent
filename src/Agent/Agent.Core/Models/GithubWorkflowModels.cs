using System;
using System.ComponentModel;

namespace Agent.Core.Models
{
    public record struct PullRequestMergeStatus(
        bool IsMerged,
        string? MergeCommitSha,
        DateTimeOffset? MergedAt,
        string State,
        string MergeableState
    );

    public record struct WorkflowRunResponse(
        long RunId,
        string Status,
        string Conclusion,
        string HtmlUrl,
        string WorkflowIdentifier
    );

    public record struct WorkflowInfo(
        long Id,
        string Name,
        string Path,
        string State,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );

    public record struct GithubWorkflowInfo(
        bool IsConnected,
        [Description("Github Repo URL")]
        string RepoUrl,
        [Description("Github Actions connected connected branch for repo")]
        string Branch,
        [Description("Path of Github Actions Workflow")]
        string WorkflowPath,
        [Description("Detail about webapp connectivity with GH Actions")]
        string Details
    );
}
