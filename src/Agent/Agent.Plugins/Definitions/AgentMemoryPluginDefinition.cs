// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
using Agent.Data.AgentMemory;
using Agent.Plugins.DataConnectors.Documentation;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class AgentMemoryPluginDefinition(IAgentMemoryClient agentMemoryClient, AgentMemorySettings agentMemorySettings, DataConnectorIndex dataConnectorindex, ILogger<AgentMemoryPluginDefinition> logger)
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

    private string BuildMemoryResponse(
        List<string> documents,
        List<string> userMemories,
        TrajectorySearchResult trajectories)
    {
        var threadId = ToolStatic.AsyncLocalThreadId.Value;
        var sb = new StringBuilder();

        if (trajectories.SameResourceTrajectories.Count > 0)
        {
            logger.LogInternalInformation("[Thread {ThreadId}] Found {Count} past incidents on the same resource",
                threadId, trajectories.SameResourceTrajectories.Count);
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
        else
        {
            logger.LogInternalInformation("[Thread {ThreadId}] No past incidents found on the same resource", threadId);
        }

        if (trajectories.SimilarSymptomsTrajectories.Count > 0)
        {
            logger.LogInternalInformation("[Thread {ThreadId}] Found {Count} past incidents with similar symptoms",
                threadId, trajectories.SimilarSymptomsTrajectories.Count);
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
        else
        {
            logger.LogInternalInformation("[Thread {ThreadId}] No past incidents found with similar symptoms", threadId);
        }

        if (userMemories.Count > 0)
        {
            logger.LogInternalInformation("[Thread {ThreadId}] Found {Count} relevant user memories",
                threadId, userMemories.Count);
            sb.AppendLine("## Related User Memories");
            sb.AppendLine();
            foreach (var memory in userMemories)
            {
                sb.AppendLine($"- {memory}");
            }
            sb.AppendLine();
        }
        else
        {
            logger.LogInternalInformation("[Thread {ThreadId}] No relevant user memories found", threadId);
        }

        if (documents.Count > 0)
        {
            logger.LogInternalInformation("[Thread {ThreadId}] Found {Count} relevant documents", threadId, documents.Count);
            sb.AppendLine("## Relevant Documents");
            sb.AppendLine();
            foreach (var doc in documents)
            {
                sb.AppendLine($"- {doc}");
            }
        }
        else
        {
            logger.LogInternalInformation("[Thread {ThreadId}] No relevant documents found", threadId);
        }

        if (sb.Length == 0)
        {
            return "No relevant memories, documents, or past incidents found for the current symptoms";
        }

        logger.LogInternalInformation("[Thread {ThreadId}] Memory response built successfully", threadId);
        return sb.ToString();
    }

    private async Task<List<string>> SearchDocumentAsync(string symptoms)
    {
        if (!agentMemorySettings.DocumentRetrievalEnabled)
        {
            logger.LogInternalInformation("Document retrieval is disabled, skipping search.");
            return [];
        }

        var documents = dataConnectorindex.SearchAsync<UserDocument>(symptoms, string.Empty, 10);

        return await documents.Select(d => d.SearchResult.Document.Chunk).ToListAsync();
    }

    private async Task<List<string>> SearchUserMemoryAsync(string symptoms)
    {
        if (!agentMemorySettings.UserMemoryRetrievalEnabled)
        {
            logger.LogInternalInformation("User memory retrieval is disabled, skipping search.");
            return [];
        }

        var memories = await agentMemoryClient.SearchUserMemoriesAsync(new SearchParams(
            Query: symptoms, K: 5, EnableHybridSearch: true, ExhaustiveKnn: true, VectorSimilarityThreshold: 0.1f));
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
            logger.LogInternalInformation("Trajectory retrieval is disabled, skipping search.");
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
