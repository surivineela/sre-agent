// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Helper class for cloning agent graphs while preserving handoff relationships.
/// </summary>
internal static class AgentGraphCloner
{
    /// <summary>
    /// Creates a deep clone of an agent graph. All agents are cloned and handoff relationships
    /// are rebuilt to point to the cloned agents.
    /// </summary>
    /// <param name="baseAgents">The base agents to clone</param>
    /// <param name="enableHandoffReasoning">Whether to enable handoff reasoning in the cloned handoffs</param>
    /// <returns>A dictionary mapping agent names to their clones</returns>
    public static Dictionary<string, Agent<TContext>> Clone<TContext>(
        IEnumerable<Agent<TContext>> baseAgents,
        bool enableHandoffReasoning = false)
        where TContext : class
    {
        var clones = new Dictionary<string, Agent<TContext>>(System.StringComparer.Ordinal);

        // Step 1: Create shallow clones (without handoffs)
        foreach (var agent in baseAgents)
        {
            clones[agent.Name] = agent.ShallowClone();
        }

        // Step 2: Rebuild handoffs pointing to cloned agents
        foreach (var agent in baseAgents)
        {
            var clone = clones[agent.Name];
            clone.Handoffs = agent.Handoffs
                .Where(h => clones.ContainsKey(h.AgentName))
                .Select(h => Handoff<TContext>.Create(
                    agent: clones[h.AgentName],
                    enableHandoffReasoning: enableHandoffReasoning))
                .ToList();
        }

        // Step 3: Rebuild AgentsAsTools pointing to cloned agents
        foreach (var agent in baseAgents)
        {
            var clone = clones[agent.Name];
            var newAgentsAsTools = new List<AgentAsTool<TContext>>();

            foreach (var agentAsTool in agent.AgentsAsTools)
            {
                // Only rebuild if the target agent exists in the cloned graph
                if (clones.ContainsKey(agentAsTool.TargetAgentName))
                {
                    var clonedTargetAgent = clones[agentAsTool.TargetAgentName];
                    var newAgentAsTool = AgentAsTool<TContext>.Create(
                        agent: clonedTargetAgent,
                        inputDescription: agentAsTool.InputDescription,
                        toolNameOverride: agentAsTool.Name,
                        toolDescriptionOverride: agentAsTool.Description,
                        maxTurns: agentAsTool.MaxTurns);
                    newAgentsAsTools.Add(newAgentAsTool);
                }
            }

            clone.AgentsAsTools = newAgentsAsTools;
        }

        return clones;
    }
}
