using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace Agent.Runtime.AgentTasks;

public class AgentTaskLocalStore
{
    private readonly InMemoryCollection<string, SearchDocumentInternal> _memoryCollection;
    private int count;

    public AgentTaskLocalStore(List<string> yamlDirectories, IEmbeddingGenerator embeddingGenerator)
    {
        var docs = LoadDocumentsFromYamlDirectories(yamlDirectories, "RCA");
        count = docs.Count;

        _memoryCollection = new InMemoryCollection<string, SearchDocumentInternal>(
            "rcaagents",
            new InMemoryCollectionOptions
            {
                EmbeddingGenerator = embeddingGenerator
            });
        _memoryCollection.EnsureCollectionExistsAsync().GetAwaiter().GetResult();

        if (count == 0)
        {
            return;
        }
        _memoryCollection.UpsertAsync(docs).GetAwaiter().GetResult();
    }

    public async IAsyncEnumerable<SearchDocument> SearchAsync(
        string query,
        int top,
        IExtendedAgentService? extendedAgentService = null)
    {
        // Update documents from the extensibility API every time since it's dynamic
        if (extendedAgentService != null)
        {
            await UpdateDocumentsFromExtApiAsync(extendedAgentService);
        }

        if (count == 0)
        {
            yield break;
        }

        await foreach (var result in _memoryCollection.SearchAsync(query, top))
        {
            yield return new SearchDocument(result.Record.Id, result.Record.Content, result.Record.Title);
        }
    }

    private async Task UpdateDocumentsFromExtApiAsync(IExtendedAgentService extendedAgentService)
    {
        var allAgents = new List<YamlAgentDescriptor>();

        // Load agents from the extensibility API
        for (int i = 0; ; i++)
        {
            var agentsFromExtensibleApi = extendedAgentService.GetAgentsAsync(i, 100, null).ConfigureAwait(true).GetAwaiter().GetResult();
            foreach (var agent in agentsFromExtensibleApi)
            {
                if (agent.Metadata.Tags?.Contains("rcaagent") == true)
                {
                    allAgents.Add(agent);
                }
            }

            if (!agentsFromExtensibleApi.HasNextPage)
                break;
        }

        var documentsFromExt = allAgents.Select(agent => new SearchDocumentInternal
        {
            Id = agent.Name,
            Title = agent.Name,
            Content = agent.Instructions
        });

        foreach (var doc in documentsFromExt)
        {
            var existing = await _memoryCollection.GetAsync(doc.Id);
            if (existing == null)
            {
                await _memoryCollection.UpsertAsync(doc);
                count++;
            }
        }
    }

    private List<SearchDocumentInternal> LoadDocumentsFromYamlDirectories(List<string> yamlDirectories, string? prefix = null)
    {
        var allAgents = YamlHelper.LoadAgentsFromYamlDirectories(yamlDirectories, prefix);
        return allAgents.Select(agent => new SearchDocumentInternal
        {
            Id = agent.Name,
            Title = agent.Name,
            Content = agent.Instructions
        }).ToList();
    }
}

internal class SearchDocumentInternal
{
    [VectorStoreKey]
    public required string Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public required string Title { get; set; }

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public required string Content { get; set; }
}
