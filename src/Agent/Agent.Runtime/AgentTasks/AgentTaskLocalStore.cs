using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace Agent.Runtime.AgentTasks;

internal class AgentTaskLocalStore
{
    private readonly InMemoryCollection<string, SearchDocumentInternal> _memoryCollection;

    public AgentTaskLocalStore(List<string> yamlDirectories, IEmbeddingGenerator embeddingGenerator)
    {
        var docs = LoadDocumentsFromYamlDirectories(yamlDirectories, "RCA");

        _memoryCollection = new InMemoryCollection<string, SearchDocumentInternal>(
            "rcaagents",
            new InMemoryCollectionOptions
            {
                EmbeddingGenerator = embeddingGenerator
            });
        _memoryCollection.EnsureCollectionExistsAsync().GetAwaiter().GetResult();
        _memoryCollection.UpsertAsync(docs).GetAwaiter().GetResult();
    }

    public async IAsyncEnumerable<SearchDocument> SearchAsync(
        string query,
        int top)
    {
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
