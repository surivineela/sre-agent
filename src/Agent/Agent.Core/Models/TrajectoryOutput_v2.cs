// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public sealed class TrajectoryOutput_v2
{
    [Description(
    """
    Analyze the conversation objectively:
    Review the user's initial request and determine if they reported a specific problem.
    Track the interaction flow, tools used, and resources mentioned.
    Evaluate whether this represents problem-solving investigation or routine operations.
    Make the determination with specific reasoning based on the criteria provided.
    """)]
    public required string ReasoningScratchPad { get; set; }

    [Description(
    """
    Boolean indicating whether this trajectory represents an investigation thread worth saving.
    Should be true only when user explicitly reports production issues and agent performs
    multi-step troubleshooting with reusable patterns.
    """)]
    public required bool IsInvestigationThread { get; set; }

    [Description(
    """
    Brief explanation of why this trajectory is or isn't classified as an investigation thread.
    For investigations: must explicitly mention the production issue reported by the user.
    For non-investigations: explain why it's routine/informational.
    """)]
    public required string ClassificationReason { get; set; }

    [Description(
    """
    Descriptive title for the trajectory that captures the main activity or outcome.
    Examples: "Function App Discovery", "Redis Connection Investigation", "Resource Health Check"
    """)]
    public required string Title { get; set; }

    [Description(
    """
    All subscription IDs mentioned or accessed during the conversation.
    Present as a semicolon-separated list.
    Example: f1ee2647-e5d4-4c50-9e76-0a42f00dc90c; ea2aa16c-c257-4359-aaea-ff2b0f3b3d10
    """)]
    public required string SubscriptionsInvolved { get; set; }

    [Description(
    """
    ALL resources mentioned or inspected in standardized queryable format.
    Format: ResourceType:ResourceName
    Present as a semicolon-separated list.
    Include container apps, databases, NSGs, subnets, VNets, storage accounts, etc.
    Example: Microsoft.Web/sites:dw-ntf-svc-1-wus2; Microsoft.Cache/redis:myredis; Microsoft.Network/networkSecurityGroups:backend-nsg
    """)]
    public required string ResourcesInvolved { get; set; }

    [Description(
    """
    System architecture or configuration facts EXPLICITLY PROVIDED BY THE USER.
    Only include information the user directly stated, not agent inferences.
    Use bullet points. Set to 'N/A' if no user-provided system knowledge was shared.
    Examples:
    - "User stated: Redis requires SSL-only connections"
    - "User confirmed: Apps are in separate subnets"
    """)]
    public required string SystemDesignKnowledge { get; set; }

    [Description(
    """
    User-reported symptoms or problems at the start of the conversation.
    Present as semicolon-separated list with short phrases, no filler words.
    Set to 'N/A' if user didn't report any specific problems.
    Example: API returns 503 errors; Login latency over 2 seconds
    """)]
    public required string InitialSymptoms { get; set; }

    [Description(
    """
    New issues or anomalies discovered during the investigation process.
    Present as semicolon-separated list, don't repeat InitialSymptoms.
    Set to 'N/A' if not an investigation or no new issues found.
    Example: SQL deadlocks detected; CPU at 95% on auth pod
    """)]
    public required string SymptomsObserved { get; set; }

    [Description(
    """
    Numbered list of diagnostic or investigative steps performed by the agent.
    Set to 'N/A' if this wasn't an investigation.
    Example:
    1. Checked application logs for errors
    2. Analyzed network connectivity between services
    3. Verified security group configurations
    """)]
    public required string StepsFollowed { get; set; }

    [Description(
    """
    Root cause explanation if definitively identified during investigation.
    Set to 'N/A' if not an investigation, or 'Unknown' if investigation didn't reach definitive RCA.
    Example: "NSG blocking Redis SSL port 6380 between subnets"
    """)]
    public required string RootCause { get; set; }

    [Description(
    """
    Actionable corrections for troubleshooting missteps, especially user corrections to agent assumptions.
    Format: "Did: [incorrect action]. Should: [correct approach]"
    Set to 'N/A' if not an investigation or no significant missteps occurred.
    Example:
    - Did: Assumed standard Redis port 6379. Should: Check if SSL is enabled (port 6380).
    """)]
    public required string Pitfalls { get; set; }
}
