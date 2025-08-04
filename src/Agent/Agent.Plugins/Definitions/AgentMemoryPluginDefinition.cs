// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using Agent.Data.AgentMemory;
using Agent.Core.Configuration;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class AgentMemoryPluginDefinition(IAgentMemoryClient agentMemoryClient, AgentMemorySettings agentMemorySettings)
{
    [Description(@"Retrieves knowledge from past memories to assist with current incident resolution")]
    public async Task<string> SearchMemoryAsync(
        [Description("The Azure resource id of the affected resource experiencing the incident")] string resourceId,
        [Description("A detailed description of the current incident symptoms, error messages, or observed behaviors")] string symptoms
    )
    {
        if (!agentMemorySettings.Enabled)
        {
            return "Agent memory is disabled, disregard this method call from the agent's context";
        }

        var trajectorySearch = SearchTrajectoriesAsync(resourceId, symptoms);
        var userMemorySearch = SearchUserMemoryAsync(symptoms);
        var documentSearch = SearchDocumentAsync(symptoms);
        await Task.WhenAll(trajectorySearch, userMemorySearch, documentSearch);
        var trajectories = await trajectorySearch;
        var userMemories = await userMemorySearch;
        var documents = await documentSearch;

        return BuildMemoryResponse(
            documents: documents,
            userMemories: userMemories,
            trajectories: trajectories
        );
    }

    private static string BuildMemoryResponse(
        List<string> documents,
        List<string> userMemories,
        TrajectorySearchResult trajectories)
    {
        var sb = new StringBuilder();

        if (trajectories.SameResourceTrajectories.Count > 0)
        {
            sb.AppendLine("## Similar Past Incidents on the exact Same Resource, which has a high likelihood of helping with the current incident resolution.");
            sb.AppendLine();
            foreach (var trajectory in trajectories.SameResourceTrajectories)
            {
                sb.AppendLine($"### {trajectory.Title}");
                sb.AppendLine($"- **Symptoms:** {trajectory.SymptomsObserved}");
                sb.AppendLine($"- **Steps followed for resolution:** {trajectory.StepsFollowed}");
                sb.AppendLine($"- **Root Cause:** {trajectory.RootCause}");
                sb.AppendLine($"- **Pitfalls to avoid:** {trajectory.Pitfalls}");
                sb.AppendLine();
            }
        }

        if (trajectories.SimilarSymptomsTrajectories.Count > 0)
        {
            sb.AppendLine("## Past Incidents with Similar Symptoms, which may provide insights into the current incident resolution.");
            sb.AppendLine();
            foreach (var trajectory in trajectories.SimilarSymptomsTrajectories)
            {
                sb.AppendLine($"### {trajectory.Title}");
                sb.AppendLine($"- **Symptoms:** {trajectory.SymptomsObserved}");
                sb.AppendLine($"- **Steps followed for resolution:** {trajectory.StepsFollowed}");
                sb.AppendLine($"- **Root Cause:** {trajectory.RootCause}");
                sb.AppendLine($"- **Pitfalls to avoid:** {trajectory.Pitfalls}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        if (userMemories.Count > 0)
        {
            sb.AppendLine("## Related User Memories");
            sb.AppendLine();
            foreach (var memory in userMemories)
            {
                sb.AppendLine($"- {memory}");
            }
            sb.AppendLine();
        }

        if (documents.Count > 0)
        {
            sb.AppendLine("## Relevant Documents");
            sb.AppendLine();
            foreach (var doc in documents)
            {
                sb.AppendLine($"- {doc}");
            }
        }

        if (sb.Length == 0)
        {
            return "No relevant memories, documents, or past incidents found for the current symptoms";
        }

        return sb.ToString();
    }

    private async Task<List<string>> SearchDocumentAsync(string symptoms)
    {
        if (!agentMemorySettings.DocumentRetrievalEnabled)
        {
            return [];
        }

        var documents = await agentMemoryClient.SearchCustomerDocumentsAsync(new SearchParams(
            Query: symptoms,
            EnableHybridSearch: true
        ));

        if (documents.Count == 0)
        {
            return [];
        }

        return documents.Select(d => d.Chunk).ToList();
    }

    private async Task<List<string>> SearchUserMemoryAsync(string symptoms)
    {
        if (!agentMemorySettings.UserMemoryRetrievalEnabled)
        {
            return [];
        }

        var memories = await agentMemoryClient.SearchUserMemoriesAsync(new SearchParams(
            Query: symptoms, K: 5, EnableHybridSearch: true, VectorSimilarityThreshold: 0.1f));
        if (memories.Count == 0)
        {
            return [];
        }

        return memories.Select(m => m.Chunk).ToList();
    }

    private record Trajectory(
        string Id,
        string Title,
        string InitialSymptoms,
        string SymptomsObserved,
        string StepsFollowed,
        string RootCause,
        string Pitfalls
    );

    private record TrajectorySearchResult(
        List<Trajectory> SameResourceTrajectories,
        List<Trajectory> SimilarSymptomsTrajectories
    );

    private async Task<TrajectorySearchResult> SearchTrajectoriesAsync(
        string resourceId,
        string symptoms)
    {
        if (!agentMemorySettings.TrajectoryRetrievalEnabled)
        {
            return new TrajectorySearchResult([], []);
        }

        // todo: put a threshold on the score to keep result relevant
        var similarSymptoms = agentMemoryClient.SearchTrajectoriesAsync(new SearchParams(Query: symptoms, K: 5, EnableHybridSearch: true));
        var pastIncidents = agentMemoryClient.SearchTrajectoriesAsync(new SearchParams(
            Query: resourceId,
            K: 5,
            EnableHybridSearch: true,
            Filter: "resource_ids/any(id: id eq '" + resourceId + "')" // todo: use case insensitive comparison if possible
        ));

        var retrieved = await Task.WhenAll(similarSymptoms, pastIncidents);
        // todo: rerank the results, e.g. using Reciprocal Rank Fusion
        if (retrieved[0].Count == 0 && retrieved[1].Count == 0)
        {
            return new TrajectorySearchResult([], []);
        }

        var sameResourceTrajectories = retrieved[1]
            .Select(x => new Trajectory(
                Id: x.Id,
                Title: x.Title,
                InitialSymptoms: x.InitialSymptoms,
                SymptomsObserved: x.SymptomsObserved,
                StepsFollowed: x.StepsFollowed,
                RootCause: x.RootCause,
                Pitfalls: x.Pitfalls))
            .ToList();

        var similarSymptomsTrajectories = retrieved[0]
            .Where(x => !sameResourceTrajectories.Any(t => t.Id == x.Id)) // avoid duplicates
            .Select(x => new Trajectory(
                Id: x.Id,
                Title: x.Title,
                InitialSymptoms: x.InitialSymptoms,
                SymptomsObserved: x.SymptomsObserved,
                StepsFollowed: x.StepsFollowed,
                RootCause: x.RootCause,
                Pitfalls: x.Pitfalls))
            .ToList();

        return new TrajectorySearchResult(
            SameResourceTrajectories: sameResourceTrajectories,
            SimilarSymptomsTrajectories: similarSymptomsTrajectories
        );
    }
}
