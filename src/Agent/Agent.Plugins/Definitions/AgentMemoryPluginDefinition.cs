// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using Agent.Data.AgentMemory;
using Agent.Core.Configuration;
using Kusto.Cloud.Platform.Utils;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class AgentMemoryPluginDefinition(IAgentMemoryClient agentMemoryClient, AgentMemorySettings agentMemorySettings)
{
    [Description("Retrieves historical incident investigation patterns and resolution strategies from past similar incidents to assist with current incident resolution.")]
    public async Task<string> GetPastIncidentKnowledge(
        [Description("The Azure resource identifier (resource ID) of the affected resource experiencing the incident")] string resourceId,
        [Description("A detailed description of the current incident symptoms, error messages, or observed behaviors")] string symptoms
    )
    {
        if (!agentMemorySettings.Enabled)
        {
            return "Agent memory is disabled, disregard this method call from the agent's context";
        }

        // todo: put a threshold on the score to keep result relevant
        var similarSymptoms = agentMemoryClient.SearchTrajectoriesAsync(symptoms, enableHybridSearch: true);
        var pastIncidents = agentMemoryClient.SearchTrajectoriesAsync(symptoms, enableHybridSearch: true, filter: $"resource_ids/any(id: id eq '{resourceId.ToLowerInvariant()}')");

        var result = await Task.WhenAll(similarSymptoms, pastIncidents);
        // todo: rerank the results, e.g. using Reciprocal Rank Fusion
        if (result[0].Count == 0 && result[1].Count == 0)
        {
            return "No relevant past incidents found.";
        }

        StringBuilder sb = new StringBuilder();

        if (result[0].Count > 0)
        {
            sb.AppendLine("Below are how past incidents with similar symptoms were investigated:");
            result[0]
                .Select((x, i) => $"{i}. Title: {x.Title}. Initial symptoms: {x.InitialSymptoms}. Symptoms: {x.SymptomsObserved}. Steps followed: {x.StepsFollowed}. Root cause: {x.RootCause}. Pitfalls: {x.Pitfalls}.")
                .ForEach(s => sb.AppendLine(s));
        }

        if (result[1].Count > 0)
        {
            sb.AppendLine("Below are how past incidents on the same resource were investigated:");
            result[1]
                .Select((x, i) => $"{i}. Title: {x.Title}. Initial symptoms: {x.InitialSymptoms}. Symptoms: {x.SymptomsObserved}. Steps followed: {x.StepsFollowed}. Root cause: {x.RootCause}. Pitfalls: {x.Pitfalls}.")
                .ForEach(s => sb.AppendLine(s));
        }

        return sb.ToString();
    }
}
