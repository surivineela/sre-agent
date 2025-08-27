using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace Agent.Runtime.AgentTasks;

public class AgentTaskLocalStore
{
    private readonly InMemoryCollection<string, SearchDocumentInternal> _memoryCollection;
    private readonly int count;

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
        int top)
    {
        if (count == 0)
        {
            yield break;
        }

        await foreach (var result in _memoryCollection.SearchAsync(query, top))
        {
            yield return new SearchDocument(result.Record.Id, result.Record.Content, result.Record.Title);
        }
    }

    private static List<SearchDocumentInternal> LoadDocumentsFromYamlDirectories(List<string> yamlDirectories, string? prefix = null)
    {
        return YamlHelper.LoadAgentsFromYamlDirectories(yamlDirectories, prefix)
            .Select(agent => new SearchDocumentInternal
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
