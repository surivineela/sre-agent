// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.Text.Json.Serialization;

namespace Agent.Data.AgentMemory;

public record SearchDocumentResult(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("chunk")] string Chunk,
    [property: JsonPropertyName("chunk_id")] string ChunkId,
    [property: JsonPropertyName("parent_id")] string ParentId,
    [property: JsonPropertyName("root_cause")] string RootCause,
    [property: JsonPropertyName("symptoms_observed")] string SymptomsObserved,
    // Note: @search.score is NOT similarity between the query and the document. For details please see https://learn.microsoft.com/en-us/azure/search/vector-search-ranking#scores-in-a-vector-search-results
    [property: JsonPropertyName("@search.score")] float SearchScore,
    [property: JsonPropertyName("@search.rerankerScore")] float RerankerScore
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
    /// <param name="query">User query</param>
    /// <param name="k">how many documents to return at most</param>
    /// <param name="vectorSimilarityThreshold">minimum vectorSimilarity theshold, valid value range [-1, 1]. Ignored if invalid value is given. Note: it is not @search.score</param>
    /// <param name="exhaustiveKnn">whether to use exhaustive knn search(slow linear search for better recall rate) or not</param>
    /// <param name="filter">odata filter for document filtering</param>
    /// <param name="enableHybridSearch">whether to enable hybrid search or not. If true, the query will be split into text and vector queries, and the results will be merged.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IList<SearchDocumentResult>> SearchCustomerDocumentsAsync(
        string query,
        uint k = 5,
        float? vectorSimilarityThreshold = null,
        bool exhaustiveKnn = false,
        string? filter = null,
        bool enableHybridSearch = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for customer documents in the agent's memory storage.
    /// </summary>
    /// <param name="query">User query</param>
    /// <param name="k">how many documents to return at most</param>
    /// <param name="vectorSimilarityThreshold">minimum vectorSimilarity theshold, valid value range [-1, 1]. Ignored if invalid value is given. Note: it is not @search.score</param>
    /// <param name="exhaustiveKnn">whether to use exhaustive knn search(slow linear search for better recall rate) or not</param>
    /// <param name="filter">odata filter for document filtering</param>
    /// <param name="enableHybridSearch">whether to enable hybrid search or not. If true, the query will be split into text and vector queries, and the results will be merged.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IList<SearchDocumentResult>> SearchTrajectoriesAsync(
        string query,
        uint k = 5,
        float? vectorSimilarityThreshold = null,
        bool exhaustiveKnn = false,
        string? filter = null,
        bool enableHybridSearch = false,
        CancellationToken cancellationToken = default);

}
