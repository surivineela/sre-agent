// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Data.AgentMemory;

public class SearchIndexService : ISearchIndexService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly ILogger<SearchIndexService> _logger;
    private readonly string _indexName;
    private readonly string _vectorizerName;
    private readonly ResourceIdentifier _managedIdentityResourceId;
    private readonly Uri _openAiEndpoint;
    private readonly string _embeddingDeploymentName;
    private readonly string _embeddingModelName;

    public SearchIndexService(
        [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryAISearchClient)] SearchClient searchClient,
        [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryIndexClient)] SearchIndexClient indexClient,
        ILogger<SearchIndexService> logger,
        AgentMemorySettings agentMemorySettings,
        OpenAISettings openAISettings)
    {
        _searchClient = searchClient;
        _indexClient = indexClient;
        _logger = logger;
        _indexName = agentMemorySettings.AzureAISearchIndexName;
        _vectorizerName = "openAIVectorizer";
        _managedIdentityResourceId = new ResourceIdentifier(agentMemorySettings.ManagedIdentityResourceId);
        _openAiEndpoint = new Uri(openAISettings.Endpoint);
        _embeddingDeploymentName = openAISettings.EmbeddingGeneratorDeploymentName;
        _embeddingModelName = openAISettings.EmbeddingGeneratorModelName;
    }


    public async Task DeleteIndexIfExistsAsync()
    {
        try
        {
            _logger.LogInternalInformation("Deleting search index...");
            var index = await _indexClient.GetIndexAsync(_indexName);

            if (!index.HasValue)
            {
                _logger.LogInternalInformation("Search index does not exist, skipping deletion.");
                return;
            }

            var searchIndex = index.Value;

            await _indexClient.DeleteIndexAsync(searchIndex);

            _logger.LogInternalInformation("Search index deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to delete search index");
        }
    }

    /// <summary>
    /// Creates or updates the search index with the current schema
    /// </summary>
    public async Task CreateOrUpdateIndexAsync()
    {
        try
        {
            _logger.LogInternalInformation("Creating/updating search index...");
            var index = GetSearchIndex();
            await _indexClient.CreateOrUpdateIndexAsync(index);
            _logger.LogInternalInformation("Search index created/updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to create or update search index");
            throw;
        }
    }

    public async Task<bool> DeleteContentsAsync(List<AgentMemory> memories)
    {
        try
        {
            if (memories == null || !memories.Any())
            {
                _logger.LogInternalInformation("No memories provided for deletion");
                return true;
            }

            var deleteActions = memories.Select(memory =>
                new IndexDocumentsAction<AgentMemory>(IndexActionType.Delete, memory)).ToArray();

            var batch = IndexDocumentsBatch.Create(deleteActions);
            var response = await _searchClient.IndexDocumentsAsync(batch);

            var allSucceeded = response.Value.Results.All(r => r.Succeeded);
            var successCount = response.Value.Results.Count(r => r.Succeeded);

            if (allSucceeded)
            {
                _logger.LogInternalInformation($"Successfully deleted {successCount} memories from search index");
                return true;
            }

            _logger.LogInternalError($"Partially failed to delete memories. {successCount}/{memories.Count} succeeded");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error deleting {memories?.Count ?? 0} memories from search index");
            return false;
        }
    }

    /// <summary>
    /// Indexes a single piece of content
    /// </summary>
    public async Task<bool> IndexContentAsync(AgentMemory content)
    {
        try
        {
            var batch = IndexDocumentsBatch.Create(
                new IndexDocumentsAction<AgentMemory>(IndexActionType.MergeOrUpload, content));
            var response = await _searchClient.IndexDocumentsAsync(batch);

            if (response.Value.Results.Any(r => r.Succeeded))
            {
                _logger.LogInternalInformation($"Successfully indexed content with ID: {content.Id}");
                return true;
            }

            _logger.LogInternalError($"Failed to index content with ID: {content.Id}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error indexing content with ID: {content.Id}");
            return false;
        }
    }

    /// <summary>
    /// Upserts a trajectory - deletes existing if present, then indexes the new content.
    /// AI Search MergeOrUpload works for most cases, but for vector updates we ensure clean replacement.
    /// </summary>
    public async Task<bool> UpsertTrajectoryAsync(AgentMemory trajectory)
    {
        try
        {
            // Check if trajectory exists and delete it first to ensure clean replacement
            var existing = await GetTrajectoryByIdAsync(trajectory.Id);
            if (existing != null)
            {
                _logger.LogInternalInformation($"Deleting existing trajectory {trajectory.Id} before upserting");
                await DeleteContentsAsync([existing]);
            }

            // Index the new/updated trajectory
            return await IndexContentAsync(trajectory);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error upserting trajectory with ID: {trajectory.Id}");
            return false;
        }
    }

    /// <summary>
    /// Gets a trajectory document by its ID from the search index
    /// </summary>
    public async Task<AgentMemory?> GetTrajectoryByIdAsync(string trajectoryId)
    {
        try
        {
            _logger.LogInternalInformation($"Retrieving trajectory with ID: {trajectoryId}");
            var response = await _searchClient.GetDocumentAsync<AgentMemory>(trajectoryId);

            if (response?.Value != null)
            {
                _logger.LogInternalInformation($"Successfully retrieved trajectory with ID: {trajectoryId}");
                return response.Value;
            }

            _logger.LogInternalWarning($"Trajectory with ID {trajectoryId} not found");
            return null;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInternalInformation($"Trajectory with ID {trajectoryId} not found in search index (404)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error retrieving trajectory with ID: {trajectoryId}");
            return null;
        }
    }

    private SearchIndex GetSearchIndex()
    {
        var fieldBuilder = new FieldBuilder
        {
            Serializer = Constants.JsonSerializer
        };

        var searchIndex = new SearchIndex(_indexName)
        {
            VectorSearch = new VectorSearch
            {
                Profiles =
                {
                    new VectorSearchProfile(Constants.VectorSearchHnswProfile, Constants.VectorSearchHnswConfig)
                    {
                        VectorizerName = _vectorizerName
                    },
                    new VectorSearchProfile(Constants.VectorSearchExhaustiveKnnProfile, Constants.VectorSearchExhaustiveKnnConfig)
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(Constants.VectorSearchHnswConfig),
                    new ExhaustiveKnnAlgorithmConfiguration(Constants.VectorSearchExhaustiveKnnConfig)
                },
                Vectorizers =
                {
                    new AzureOpenAIVectorizer(_vectorizerName)
                    {
                        Parameters = new AzureOpenAIVectorizerParameters()
                        {
                            ResourceUri = _openAiEndpoint,
                            DeploymentName = _embeddingDeploymentName,
                            ModelName = _embeddingModelName,
                            AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(_managedIdentityResourceId)
                        }
                    }
                }
            },
            SemanticSearch = new SemanticSearch
            {
                Configurations =
                {
                    new SemanticConfiguration(Constants.SemanticSearchConfig, new()
                    {
                        TitleField = new SemanticField(fieldName: "title"),
                        ContentFields =
                        {
                            new SemanticField(fieldName: "chunk")
                        },
                    })
                },
            },
            Fields = fieldBuilder.Build(typeof(AgentMemory)),
        };

        return searchIndex;
    }
}
