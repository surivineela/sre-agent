// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.ThreadEvaluator;

public sealed class TrajectoryOutput
{
    [Description(
        """
        Use this space to think step-by-step about the chat transcript and what insights may be drawn from it.
        A detailed reasoning is imperative to draw the right conclusions.

        Your thinking process to extract details may look like:
        ──────────────────
        • Quick line-number notes: “L12-15 look like initial symptom candidate”.
        • Hypotheses and open questions: “Is ‘error 18456’ user-auth or db-auth?”.
        • Ambiguity resolutions: “Treat ‘timeout’ in L47 as observed symptom; not in initial list.”
        • Temporary buckets or tallies before you deduplicate / reformat.
        • TODO markers: “Need one more pitfall; scan lines 90-120 again.”

        What **not** to write
        ─────────────────────
        × Don’t narrate incident details for the user.
        """)]
    public required string ReasoningScratchPad { get; set; }

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
        Ordered list or decision-tree of investigative steps, including the tool
        used for each step.  Indentation or arrows (“├─”) may be used to show
        branching, but the entire structure must be encoded in this single string.
        """)]
    public required string StepsFollowed { get; set; }

    [Description("""
        If a root cause was found, give a concise explanation here.
        If no definitive RCA was reached, set to "Unknown"
        """)]
    public required string RootCause { get; set; }

    [Description(
        """
        Any system-design explanations, background theory, or general knowledge
        that appeared in the chat and could help future investigations.
        Use bullet points.
        Eg:
        - app A talks to app B for auth.
        - app A is run in active-active format.
        If no knowledge mentioned, set to "None"
        """)]
    public required string SystemDesignKnowledge { get; set; }

    [Description(
        """
        Concrete resources inspected (e.g. resource names, hostnames).
        Present in a semicolon separated list.
        For resources present complete Azure Resource ID.
        Example: /subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/prod-shopping-c1; auth.contoso.com
        """)]
    public required string ResourcesInvolved { get; set; }

    [Description(
        """
        Resource *types* touched
        deduplicated and presented in a semicolon separated list.
        Example: Azure App Service; SQL Database
        """)]
    public string? ResourceTypesInvolved { get; set; }

    [Description(
        """
        Missteps, dead-ends, wrong inferences, or other pitfalls encountered by the assistant.
        """)]
    public string? Pitfalls { get; set; }
}
