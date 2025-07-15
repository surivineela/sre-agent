// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public sealed class TrajectoryOutput_v2
{
    [Description(
    """
    Think through the chat like you're reviewing an incident post-mortem:
    Start with the story arc:
    "User came in with X symptom.. Agent initially thought Y.. Discovered Z through [tool]..."
    Track the investigation flow:
    "L23: Agent used kubectl → found pods crashing"
    "L45: This triggered NSG investigation because..."
    "L67: User corrected: 'No, we use port 6380 for SSL' ← Important pitfall!"
    Systematically extract resources:
    "Container app mentioned: L12 'dashboard'"
    "Redis instance: L34 'rfpgunrsght2eiot'"
    "NSG discovered: L56 during 'az network nsg list'"
    Note user-provided system facts:
    "L78: User states 'our Redis requires SSL connections only'"
    "L89: User confirms 'yes, they're in different subnets'"
    Identify reusable patterns:
    "This follows the classic connectivity troubleshooting pattern: app → network path → target"
    "Key learning: Always check NSGs on BOTH ends of connection"
    Judge trajectory value:
    "Complete investigation? ✓ Root cause found? ✓ Reusable pattern? ✓ → Worth saving"
    """)]
    public required string ReasoningScratchPad { get; set; }

    [Description(
        """
        Boolean indicating whether this trajectory represents an investigation thread worth saving.
        Should be true for multi-step investigations, root cause analysis,
        system insights, or reusable troubleshooting patterns.
        """)]
    public required bool IsInvestigationThread { get; set; }

    [Description(
        """
        Brief explanation of why this trajectory is or isn't classified as an investigation thread.
        """)]
    public required string InvestigationReason { get; set; }

    [Description(
        """
        Title for the trajectory
        """)]
    public required string Title { get; set; }

    [Description(
        """
        User-reported symptoms at the very start of the chat.
        Present a semicolon separated list with one short phrase per symptom.
        Strip filler words. Keep it crisp.
        Example: auth API returns 503; Latency > 2 s on login
        """)]
    public required string InitialSymptoms { get; set; }

    [Description(
        """
        New signals or anomalies discovered *during* the investigation.
        Present a semicolon separated list with one short phrase per symptom.
        Do NOT repeat anything already listed in InitialSymptoms.
        Example: SQL deadlocks on OrdersDB; CPU 95 % on auth-service pod
        """)]
    public required string SymptomsObserved { get; set; }

    [Description(
        """
        Numbered list of investigative steps performed by the agent.
        """)]
    public required string StepsFollowed { get; set; }

    [Description("""
        If a root cause was found, give a concise explanation here.
        If no definitive RCA was reached, set to "Unknown"
        """)]
    public required string RootCause { get; set; }

    [Description(
        """
        Any system-design explanations that were EXPLICITLY PROVIDED BY THE USER in the chat.
        Only include information the user directly stated, not inferences.
        Use bullet points.
        Examples:
        - "User stated: Redis is configured for SSL-only connections"
        - "User confirmed: Container app and Redis are in separate subnets"
        - "User mentioned: We use active-active deployment for the dashboard app"
        If no user-provided knowledge mentioned, set to "None"
        """)]
    public required string SystemDesignKnowledge { get; set; }

    [Description(
    """
        All subscription IDs involved in the investigation.
        Present as a semicolon-separated list.
        Example: f1ee2647-e5d4-4c50-9e76-0a42f00dc90c; ea2aa16c-c257-4359-aaea-ff2b0f3b3d10
        """)]
    public required string SubscriptionsInvolved { get; set; }

    [Description(
        """
        ALL resources inspected in standardized queryable format.
        Format: ResourceType:ResourceName
        Present as a semicolon-separated list.
        Must include EVERY resource mentioned: container apps, databases, NSGs, subnets, VNets, etc.
        Example: Microsoft.App/containerApps:dashboard; Microsoft.Cache/redis:rfpgunrsght2eiot; Microsoft.Network/networkSecurityGroups:NRMS-rfpgunrsght2eiot-dashboard-vnet-redis-subnet; Microsoft.Network/virtualNetworks/subnets:iot-dashboard-vnet/redis-subnet
        """)]
    public required string ResourcesInvolved { get; set; }

    [Description(
        """
        Actionable corrections for missteps encountered.
        Format each as: "Did: [what was done wrong]. Should: [correct approach instead]"
        One bullet per pitfall.
        Focus on user corrections to the model.
        Example:
        - Did: Checked NSG rules only on container app subnet. Should: Always check NSG rules on both source AND destination subnets.
        - Did: Assumed port 6379 for Redis SSL. Should: Use port 6380 for Redis SSL connections, port 6379 for non-SSL.
        """)]
    public required string Pitfalls { get; set; }
}
