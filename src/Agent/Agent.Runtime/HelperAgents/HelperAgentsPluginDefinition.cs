// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.HelperAgents;

public class HelperAgentsPluginDefinition
{
    public static IReadOnlyList<string> AllPluginNames => [.. typeof(HelperAgentsPluginDefinition).GetMethods().Select(m => m.Name)];

    [HelperAgentPlugin(AgentInputType = typeof(DiagnosisAgentInput))]
    [Description("Start a diagnosis or investigation on the Azure resource specified by the resourceId to investigate or develop a hypothesis for a potential cause of the issue.")]
    public string StartDiagnosisAgent(
        [Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")]
        string resourceId,
        [Description("Detailed description of the issue to diagnose, including additional information gathered so far")]
        string issueDescription)
    {
        throw new Exception("Helper agent plugin should not be invoked directly.");
    }
}
