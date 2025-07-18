// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public sealed class TrajectoryOutput_v3
{
    [Description(
    """
    Analyze the conversation objectively:
    Review the user's initial request and determine if they reported a specific problem.
    Track the interaction flow, tools used, and resources mentioned.
    Evaluate whether this represents problem-solving investigation or routine operations.
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
    If the user explicitly provides an incident title or live‑site alert name, record it verbatim.
    Use "N/A" if not provided.
    """)]
    public required string IncidentTitle { get; set; }

    [Description(
    """
    If the user explicitly provides an incident identifier, record it verbatim.
    Use "N/A" if not provided.
    """)]
    public required string IncidentID { get; set; }

    [Description(
    """
    Timestamp of the incident if explicitly mentioned by the user.
    Use "N/A" if not provided.
    """)]
    public required string IncidentTime { get; set; }

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
    Numbered list of diagnostic or investigative steps that advanced the investigation.
    Each step should include agent name, action/intent, and outcome, e.g.:
      1. **meta_agent** – Call `transfer_to_kubernetes_agent` to examine pods → `kubernetes_agent` takes control
    Use "N/A" if this wasn't an investigation.
    """)]
    public required string StepsFollowed { get; set; }

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
    Actionable corrections for troubleshooting missteps, especially user corrections to agent assumptions.
    Format: - "Did: [incorrect action]. Should: [correct approach]"
    Set to 'N/A' if not an investigation or no significant missteps occurred.
    Separate multiple pitfalls with newlines.
    Example:
    - Did: Checked application logs first. Should: Call `transfer_to_network_diagnostics_agent` to verify connectivity before application layer
    - Did: Used generic `SearchResourceByName` without filters. Should: Use `ListResourcesByType` with subscription/resource group parameters
    """)]
    public required string Pitfalls { get; set; }

    [Description(
    """
    Root cause explanation if definitively identified during investigation.
    Set to 'N/A' if not an investigation.
    Example values:
    - Confirmed by User: NSG rule blocking Redis SSL port 6380 between subnets
    - Unconfirmed: ACR firewall may be blocking new GPU subnet
    - Inconclusive investigation: Ruled out authentication failures; network path still suspect
    """)]
    public required string RootCause { get; set; }

    [Description(
    """
    ALL resources involved in investigation. Mention complete ARM resource id.
    Present as a semicolon-separated list.
    Include container apps, databases, NSGs, subnets, VNets, storage accounts, etc.
    Example: /subscriptions/f1ee2647-e5d4-4c50-9e76-0a42f00dc90c/resourcegroups/aca-sre-agent-demo/providers/microsoft.app/containerapps/iot-dashboard; /subscriptions/f1ee2647-e5d4-4c50-9e76-0a42f00dc90c/resourceGroups/aca-sre-agent-demo/providers/Microsoft.Network/virtualNetworks/iot-dashboard-vnet/subnets/container-apps-subnet
    """)]
    public required string ResourcesInvolved { get; set; }
}
