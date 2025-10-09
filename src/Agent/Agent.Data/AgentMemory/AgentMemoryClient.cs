// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Agent.Data.AgentMemory.Constants;

namespace Agent.Data.AgentMemory;

public class AgentMemoryClient : IAgentMemoryClient
{
    private readonly ILogger<AgentMemoryClient> _logger;
    private readonly BlobServiceClient _agentBlobClient;
    private readonly SearchIndexerClient _indexerClient;
    private readonly SearchClient _searchClient;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AgentMemorySettings _agentMemorySettings;
    private readonly OpenAISettings _openAISettings;
    private readonly ISearchIndexService _searchIndexService;

    private readonly string _blobContainerName;
    private readonly string _aiSearchDataSourceName;
    private readonly string _aiSearchIndexName;
    private readonly string _aiSearchIndexerName;
    private readonly string _aiSearchSkillsetName;

    // Maximum number of results to return in a search query
    private const uint maxK = 100;

    public AgentMemoryClient(
        ILogger<AgentMemoryClient> logger,
        [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryBlobClient)] BlobServiceClient agentBlobClient,
        [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryIndexerClient)] SearchIndexerClient indexerClient,
        [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryAISearchClient)] SearchClient searchClient,
        IHostEnvironment hostEnvironment,
        AgentMemorySettings agentMemorySettings,
        OpenAISettings openAISettings,
        ISearchIndexService searchIndexService)
    {
        _logger = logger;
        _agentBlobClient = agentBlobClient;
        _indexerClient = indexerClient;
        _searchClient = searchClient;
        _hostEnvironment = hostEnvironment;
        _agentMemorySettings = agentMemorySettings;
        _openAISettings = openAISettings;
        _searchIndexService = searchIndexService;

        _blobContainerName = _agentMemorySettings.BlobStorageContainerName;
        _aiSearchDataSourceName = _agentMemorySettings.AzureAISearchDataSourceName;
        _aiSearchIndexName = _agentMemorySettings.AzureAISearchIndexName;
        _aiSearchIndexerName = _agentMemorySettings.AzureAISearchIndexerName;
        _aiSearchSkillsetName = _agentMemorySettings.AzureAISearchSkillSetName;
    }

    async Task<bool> IAgentMemoryClient.UploadDocumentAsync(string fileName, Stream documentStream)
    {
        if (!_agentMemorySettings.StorageAccountEnabled)
        {
            _logger.LogInternalWarning($"NoOp on UploadDocumentAsync as storage account is disabled");
            return false;
        }

        // Validation checks
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogInternalError("Upload failed: fileName is null or empty");
            return false;
        }

        if (documentStream == null)
        {
            _logger.LogInternalError("Upload failed: documentStream is null");
            return false;
        }

        if (documentStream.Length == 0)
        {
            _logger.LogInternalError($"Upload failed: documentStream for '{fileName}' is empty");
            return false;
        }

        if (!documentStream.CanRead)
        {
            _logger.LogInternalError($"Upload failed: documentStream for '{fileName}' is not readable");
            return false;
        }

        try
        {
            _logger.LogInternalInformation($"Uploading document '{fileName}' ({documentStream.Length} bytes) to container '{_blobContainerName}'");

            var blobClient = _agentBlobClient.GetBlobContainerClient(_blobContainerName)
                .GetBlobClient(fileName);

            var response = await blobClient.UploadAsync(documentStream, overwrite: true);

            if (response?.Value != null)
            {
                _logger.LogInternalInformation($"Successfully uploaded document '{fileName}' to container '{_blobContainerName}'. ETag: {response.Value.ETag}");
                return true;
            }
            else
            {
                _logger.LogInternalError($"Upload failed: No response received for document '{fileName}'");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Upload failed: Exception occurred while uploading document '{fileName}': {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteDocumentAsync(string fileName)
    {
        if (!_agentMemorySettings.StorageAccountEnabled)
        {
            _logger.LogInternalWarning($"Delete operation skipped: Storage account is disabled for file '{fileName}'");
            return false;
        }

        // Validation checks
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogInternalError("Delete failed: fileName is null or empty");
            return false;
        }

        try
        {
            _logger.LogInternalInformation($"Attempting to delete document '{fileName}' from container '{_blobContainerName}'");

            var blobClient = _agentBlobClient.GetBlobContainerClient(_blobContainerName)
                .GetBlobClient(fileName);

            // Check if blob exists first
            var existsResponse = await blobClient.ExistsAsync();
            if (!existsResponse.Value)
            {
                _logger.LogInternalWarning($"Delete failed: Document '{fileName}' does not exist in container '{_blobContainerName}'");
                return false;
            }

            // Delete the blob
            var deleteResponse = await blobClient.DeleteAsync();

            // If we get here without an exception, the delete was successful
            _logger.LogInternalInformation($"Successfully deleted document '{fileName}' from container '{_blobContainerName}'");
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInternalWarning($"Delete failed: Document '{fileName}' not found (404)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Delete failed: Exception occurred while deleting document '{fileName}': {ex.Message}");
            return false;
        }
    }

    public async Task SetupIndexerAsync()
    {
        await CreateOrUpdateIndexerAsync();
    }

    public async Task RunIndexerAsync()
    {
        if (!_agentMemorySettings.StorageAccountEnabled)
        {
            _logger.LogInternalWarning($"NoOp on {nameof(RunIndexerAsync)} as storage account is disabled");
            return;
        }

        _logger.LogInternalInformation("Running the indexer...");
        await _indexerClient.RunIndexerAsync(_aiSearchIndexerName).ConfigureAwait(false);
        _logger.LogInternalInformation("Indexer is Running!");
    }

    private async Task CreateOrUpdateIndexerAsync()
    {
        try
        {
            // Create or update the search index
            _logger.LogInternalInformation("Creating/updating search index...");
            await _searchIndexService.CreateOrUpdateIndexAsync();
            _logger.LogInternalInformation("Search index created/updated successfully");

            if (!_agentMemorySettings.StorageAccountEnabled)
            {
                _logger.LogInternalWarning($"Skipping other subresource update {nameof(CreateOrUpdateIndexerAsync)} as storage account is disabled");
                return;
            }

            // Create or update data source connection
            _logger.LogInternalInformation("Creating/updating data source connection...");
            var dataSource = new SearchIndexerDataSourceConnection(
                _aiSearchDataSourceName,
                SearchIndexerDataSourceType.AzureBlob,
                connectionString: $"ResourceId={_agentMemorySettings.BlobStorageResourceId}",
                container: new SearchIndexerDataContainer(_blobContainerName));

            // Enable native blob soft delete detection
            dataSource.DataDeletionDetectionPolicy = new NativeBlobSoftDeleteDeletionDetectionPolicy();

            dataSource.IndexerPermissionOptions ??= new List<IndexerPermissionOption>();
            dataSource.Identity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(_agentMemorySettings.ManagedIdentityResourceId));
            await _indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);
            _logger.LogInternalInformation("Data source connection created/updated successfully");

            // Create or update skillset
            _logger.LogInternalInformation("Creating/updating skillset...");
            var skillset = new SearchIndexerSkillset(_aiSearchSkillsetName, new List<SearchIndexerSkill>
            {
                new SplitSkill(
                    new List<InputFieldMappingEntry>
                    {
                        new InputFieldMappingEntry("text") { Source = "/document/content" }
                    },
                    new List<OutputFieldMappingEntry>
                    {
                        new OutputFieldMappingEntry("textItems") { TargetName = "pages" }
                    })
                {
                    Context = "/document",
                    TextSplitMode = TextSplitMode.Pages,
                    MaximumPageLength = 2000,
                    PageOverlapLength = 500,
                },
                new AzureOpenAIEmbeddingSkill(
                    new List<InputFieldMappingEntry>
                    {
                        new InputFieldMappingEntry("text") { Source = "/document/pages/*" }
                    },
                    new List<OutputFieldMappingEntry>
                    {
                        new OutputFieldMappingEntry("embedding") { TargetName = "vector" }
                    }
                )
                {
                    Context = "/document/pages/*",
                    ResourceUri = new Uri(_openAISettings.Endpoint),
                    DeploymentName = _openAISettings.EmbeddingGeneratorDeploymentName,
                    ModelName = _openAISettings.EmbeddingGeneratorModelName,
                    AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(_agentMemorySettings.ManagedIdentityResourceId))
                },
                // https://learn.microsoft.com/en-us/azure/search/cognitive-search-skill-conditional
                new ConditionalSkill(
                    new List<InputFieldMappingEntry>
                    {
                        new InputFieldMappingEntry("condition") { Source = "= true" },
                        new InputFieldMappingEntry("whenTrue") { Source = "= 'document'" },
                        new InputFieldMappingEntry("whenFalse") { Source = "= null" } // should never happen because condition is always true
                    },
                    new List<OutputFieldMappingEntry>
                    {
                        new OutputFieldMappingEntry("output") { TargetName = "documentType" }
                    })
                {
                    Context = "/document",
                }
            })
            {
                IndexProjection = new SearchIndexerIndexProjection(new[]
                {
                    new SearchIndexerIndexProjectionSelector(_aiSearchIndexName, parentKeyFieldName: "parent_id", sourceContext: "/document/pages/*", mappings: new[]
                    {
                        new InputFieldMappingEntry("chunk")
                        {
                            Source = "/document/pages/*"
                        },
                        new InputFieldMappingEntry("vector")
                        {
                            Source = "/document/pages/*/vector"
                        },
                        new InputFieldMappingEntry("title")
                        {
                            Source = "/document/metadata_storage_name"
                        },
                        new InputFieldMappingEntry("type")
                        {
                            Source = "/document/documentType"
                        },
                    })
                })
                {
                    Parameters = new SearchIndexerIndexProjectionsParameters
                    {
                        ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
                    }
                }
            };
            await _indexerClient.CreateOrUpdateSkillsetAsync(skillset);
            _logger.LogInternalInformation("Skillset created/updated successfully");

            // Create or update indexer
            _logger.LogInternalInformation("Creating/updating indexer...");
            var indexer = new SearchIndexer(_aiSearchIndexerName, dataSource.Name, _aiSearchIndexName)
            {
                Description = "Indexer to chunk documents, generate embeddings, and add to the index",
                Schedule = new IndexingSchedule(TimeSpan.FromDays(1))
                {
                    StartTime = DateTimeOffset.Now
                },
                Parameters = new IndexingParameters()
                {
                    BatchSize = 1,
                    MaxFailedItems = 0,
                    MaxFailedItemsPerBatch = 0,
                },
                SkillsetName = skillset.Name,
            };
            await _indexerClient.CreateOrUpdateIndexerAsync(indexer);
            _logger.LogInternalInformation("Indexer created/updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to create or update Azure AI Search indexer infrastructure");
            throw;
        }
    }

    private async Task<IList<SearchDocumentResult>> SearchAsync(SearchParams searchParams, CancellationToken cancellationToken)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Filter = searchParams.Filter ?? "",
                Size = (int)Math.Min(searchParams.K, maxK),
                Select = { "id", "title", "chunk", "root_cause", "symptoms_observed", "initial_symptoms", "steps_followed", "resource_types", "resource_ids", "pitfalls", "indexed_at", },
                IncludeTotalCount = true,
                VectorSearch = new VectorSearchOptions(),
            };

            searchOptions.VectorSearch.Queries.Add(new VectorizableTextQuery(searchParams.Query)
            {
                KNearestNeighborsCount = (int)Math.Min(searchParams.K, maxK),
                Exhaustive = searchParams.ExhaustiveKnn,
                Fields = { "vector" },
                Threshold = searchParams.VectorSimilarityThreshold.HasValue && searchParams.VectorSimilarityThreshold.Value >= -1 && searchParams.VectorSimilarityThreshold.Value <= 1
                    ? new VectorSimilarityThreshold(searchParams.VectorSimilarityThreshold.Value)
                    : null,
            });

            searchOptions.QueryType = searchParams.EnableSemanticSearch ? SearchQueryType.Semantic : SearchQueryType.Simple;
            searchOptions.SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = Constants.SemanticSearchConfig,
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive)
            };

            var response = await _searchClient.SearchAsync<SearchDocumentResult>(
                searchText: searchParams.EnableHybridSearch ? searchParams.Query : "*",
                options: searchOptions,
                cancellationToken: cancellationToken);
            var results = new List<SearchDocumentResult>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                _logger.LogInternalInformation($"Found document: {result.Document.Title} with chunk_id: {result.Document.ChunkId}, parent_id: {result.Document.ParentId}, score: {result.Document.SearchScore}, reranker score: {result.Document.RerankerScore}");
                results.Add(result.Document);
            }

            return results;

        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Search failed: {ex.Message}");
            return [];
        }
    }

    public async Task<IList<SearchDocumentResult>> SearchCustomerDocumentsAsync(
        SearchParams searchParams,
        CancellationToken cancellationToken = default)
    {
        var additionalFilter = string.IsNullOrEmpty(searchParams.Filter) ? "" : $"and ({searchParams.Filter})";
        return await SearchAsync(searchParams with { Filter = $"type eq '{AgentMemoryType.Document.ToLowerString()}' {additionalFilter}" }, cancellationToken);
    }

    public async Task<IList<SearchDocumentResult>> SearchTrajectoriesAsync(
        SearchParams searchParams,
        CancellationToken cancellationToken = default)
    {
        var additionalFilter = string.IsNullOrEmpty(searchParams.Filter) ? "" : $"and ({searchParams.Filter})";
        return await SearchAsync(searchParams with { Filter = $"type eq '{AgentMemoryType.Trajectory.ToLowerString()}' {additionalFilter}" }, cancellationToken);
    }

    public async Task<IList<SearchDocumentResult>> SearchUserMemoriesAsync(
        SearchParams searchParams,
        CancellationToken cancellationToken = default)
    {
        var additionalFilter = string.IsNullOrEmpty(searchParams.Filter) ? "" : $"and ({searchParams.Filter})";
        return await SearchAsync(searchParams with { Filter = $"type eq '{AgentMemoryType.UserMemory.ToLowerString()}' {additionalFilter}" }, cancellationToken);
    }

    public async Task<BlobListPage> ListFilesAsync(string? prefix = null, int? pageSize = null, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        if (!_agentMemorySettings.StorageAccountEnabled)
        {
            _logger.LogInternalWarning($"NoOp on ListFilesAsync as storage account is disabled");
            return new BlobListPage(Array.Empty<string>(), null);
        }

        try
        {
            var containerClient = _agentBlobClient.GetBlobContainerClient(_blobContainerName);

            var exists = await containerClient.ExistsAsync();
            if (!exists.Value)
            {
                _logger.LogInternalWarning($"ListFiles failed: Container '{_blobContainerName}' does not exist");
                return new BlobListPage(Array.Empty<string>(), null);
            }

            var items = new List<string>();
            var pageable = containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken);
            string? nextToken = null;

            await foreach (var page in pageable.AsPages(continuationToken, pageSize))
            {
                foreach (var blobItem in page.Values)
                {
                    // blobItem.Deleted is already excluded by BlobStates.None
                    items.Add(blobItem.Name);
                }

                nextToken = page.ContinuationToken; // null when no more pages
                break;
            }

            return new BlobListPage(items, nextToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to list files: {ex.Message}");
            return new BlobListPage(Array.Empty<string>(), null);
        }
    }
}
