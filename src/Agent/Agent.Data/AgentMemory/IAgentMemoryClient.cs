// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.Text.Json.Serialization;

namespace Agent.Data.AgentMemory;

public record SearchDocumentResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("chunk")] string Chunk,
    [property: JsonPropertyName("chunk_id")] string ChunkId,
    [property: JsonPropertyName("parent_id")] string ParentId,
    [property: JsonPropertyName("root_cause")] string RootCause,
    [property: JsonPropertyName("symptoms_observed")] string SymptomsObserved,
    [property: JsonPropertyName("initial_symptoms")] string InitialSymptoms,
    [property: JsonPropertyName("steps_followed")] string StepsFollowed,
    [property: JsonPropertyName("resource_types")] IList<string> ResourceTypes,
    [property: JsonPropertyName("resource_ids")] IList<string> ResourceIds,
    [property: JsonPropertyName("pitfalls")] string Pitfalls,
    [property: JsonPropertyName("indexed_at")] DateTime IndexedAt,
    // Note: @search.score is NOT similarity between the query and the document. For details please see https://learn.microsoft.com/en-us/azure/search/vector-search-ranking#scores-in-a-vector-search-results
    [property: JsonPropertyName("@search.score")] float SearchScore,
    [property: JsonPropertyName("@search.rerankerScore")] float RerankerScore
);

public record SearchParams(
    string Query, // query to search for
    uint K = 5, // how many documents to return at most
    float? VectorSimilarityThreshold = null, // minimum vectorSimilarity theshold, valid value range [-1, 1]. Ignored if invalid value is given. Note: it is not @search.score
    bool ExhaustiveKnn = false, // whether to use exhaustive knn search(slow linear search for better recall rate) or not
    string? Filter = null, // extra odata filter for document filtering
    bool EnableHybridSearch = false, // whether to enable hybrid search or not. If true, the query will be split into text and vector queries, and the results will be merged.
    bool EnableSemanticSearch = false // whether to enable semantic search or not. If true, the query will be processed using semantic search capabilities.
);

public interface IAgentMemoryClient
{
    /// <summary>
    /// Uploads documents to the agent's memory storage.
    /// Will overwrite if a document with the same name already exists.
    /// </summary>
    /// <param name="fileName">fileName of the document</param>
    /// <param name="documentStream">document content stream. The caller is responsible of closing the stream</param>
    /// <returns>True if upload was successful, false otherwise</returns>
    Task<bool> UploadDocumentAsync(string fileName, Stream documentStream);

    Task SetupIndexerAsync();

    Task RunIndexerAsync();

    /// <summary>
    /// Searches for customer documents in the agent's memory storage.
    /// </summary>
    /// <param name="searchParams">Search parameters</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IList<SearchDocumentResult>> SearchCustomerDocumentsAsync(
        SearchParams searchParams,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for past incident trajectories in the agent's memory storage.
    /// </summary>
    /// <param name="searchOptions">Search options</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IList<SearchDocumentResult>> SearchTrajectoriesAsync(
        SearchParams searchOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for user memories uploaded using `#remember` in the agent's memory storage.
    /// </summary>
    /// <param name="searchOptions">Search options</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IList<SearchDocumentResult>> SearchUserMemoriesAsync(
        SearchParams searchOptions,
        CancellationToken cancellationToken = default);

}
