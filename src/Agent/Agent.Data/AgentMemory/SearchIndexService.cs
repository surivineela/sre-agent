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

    /// <summary>
    /// Indexes a single piece of content
    /// </summary>
    public async Task<bool> IndexContentAsync(BaseIndexableContent content)
    {
        try
        {
            var searchDocument = content.ToSearchDocument();
            var batch = IndexDocumentsBatch.Create(
                new IndexDocumentsAction<SearchDocument>(IndexActionType.MergeOrUpload, searchDocument));
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
