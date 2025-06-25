// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Logging;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Data.AgentMemory;

public class AgentMemoryClient(ILogger<AgentMemoryClient> logger,
                               [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryBlobClient)] BlobServiceClient agentBlobClient,
                               [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryIndexClient)] SearchIndexClient indexClient,
                               [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryIndexerClient)] SearchIndexerClient indexerClient,
                               [FromKeyedServices(AgentMemoryConfiguration.AgentMemoryAISearchClient)] SearchClient searchClient,
                               IHostEnvironment hostEnvironment,
                               AgentMemorySettings agentMemorySettings,
                               OpenAISettings openAISettings) : IAgentMemoryClient
{
    private readonly string blobContainerName = AgentNameHelper.GetCustomerUploadedDocumentBlobContainerName(hostEnvironment.IsProduction());
    private readonly string aiSearchDataSourceName = AgentNameHelper.GetCustomerUploadedDocumentAISearchDatasourceName(hostEnvironment.IsProduction());
    private readonly string aiSearchIndexName = AgentNameHelper.GetCustomerUploadedDocumentIndexName(hostEnvironment.IsProduction());
    private readonly string aiSearchIndexerName = AgentNameHelper.GetCustomerUploadedDocumentIndexerName(hostEnvironment.IsProduction());
    private readonly string aiSearchSkillsetName = AgentNameHelper.GetCustomerUploadedDocumentSkillSetName(hostEnvironment.IsProduction());
    private const string semanticSearchConfig = "semanticConfig";
    // Maximum number of results to return in a search query
    private const uint maxK = 100;

    async Task<bool> IAgentMemoryClient.UploadDocumentAsync(string fileName, Stream documentStream)
    {
        // Validation checks
        if (string.IsNullOrWhiteSpace(fileName))
        {
            logger.LogInternalError("Upload failed: fileName is null or empty");
            return false;
        }

        if (documentStream == null)
        {
            logger.LogInternalError("Upload failed: documentStream is null");
            return false;
        }

        if (documentStream.Length == 0)
        {
            logger.LogInternalError($"Upload failed: documentStream for '{fileName}' is empty");
            return false;
        }

        if (!documentStream.CanRead)
        {
            logger.LogInternalError($"Upload failed: documentStream for '{fileName}' is not readable");
            return false;
        }

        try
        {
            logger.LogInternalInformation($"Uploading document '{fileName}' ({documentStream.Length} bytes) to container '{blobContainerName}'");

            var blobClient = agentBlobClient.GetBlobContainerClient(AgentNameHelper.GetCustomerUploadedDocumentBlobContainerName(hostEnvironment.IsProduction()))
                .GetBlobClient(fileName);

            var response = await blobClient.UploadAsync(documentStream, overwrite: true);

            if (response?.Value != null)
            {
                logger.LogInternalInformation($"Successfully uploaded document '{fileName}' to container '{blobContainerName}'. ETag: {response.Value.ETag}");
                return true;
            }
            else
            {
                logger.LogInternalError($"Upload failed: No response received for document '{fileName}'");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, $"Upload failed: Exception occurred while uploading document '{fileName}': {ex.Message}");
            return false;
        }
    }

    public async Task SetupIndexerAsync()
    {
        await CreateOrUpdateIndexerAsync();
    }

    public async Task RunIndexerAsync()
    {
        logger.LogInternalInformation("Running the indexer...");
        await indexerClient.RunIndexerAsync(aiSearchIndexerName).ConfigureAwait(false);
        logger.LogInternalInformation("Indexer is Running!");
    }

    internal async Task CreateOrUpdateIndexerAsync()
    {
        try
        {
            // Create or update the search index
            logger.LogInternalInformation("Creating/updating search index...");
            var index = GetDocumentIndex();
            await indexClient.CreateOrUpdateIndexAsync(index);
            logger.LogInternalInformation("Search index created/updated successfully");

            // Create or update data source connection
            logger.LogInternalInformation("Creating/updating data source connection...");
            var dataSource = new SearchIndexerDataSourceConnection(
                aiSearchDataSourceName,
                SearchIndexerDataSourceType.AzureBlob,
                connectionString: $"ResourceId={agentMemorySettings.BlobStorageResourceId}",
                container: new SearchIndexerDataContainer(blobContainerName));

            dataSource.IndexerPermissionOptions ??= new List<IndexerPermissionOption>();
            dataSource.Identity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(agentMemorySettings.ManagedIdentityResourceId));
            await indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);
            logger.LogInternalInformation("Data source connection created/updated successfully");

            // Create or update skillset
            logger.LogInternalInformation("Creating/updating skillset...");
            var skillset = new SearchIndexerSkillset(aiSearchSkillsetName, new List<SearchIndexerSkill>
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
                    ResourceUri = new Uri(openAISettings.Endpoint),
                    DeploymentName = openAISettings.EmbeddingGeneratorDeploymentName,
                    ModelName = openAISettings.EmbeddingGeneratorModelName,
                    AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(agentMemorySettings.ManagedIdentityResourceId))
                }
            })
            {
                IndexProjection = new SearchIndexerIndexProjection(new[]
                {
                    new SearchIndexerIndexProjectionSelector(index.Name, parentKeyFieldName: "parent_id", sourceContext: "/document/pages/*", mappings: new[]
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
                        }
                    })
                })
                {
                    Parameters = new SearchIndexerIndexProjectionsParameters
                    {
                        ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
                    }
                }
            };
            await indexerClient.CreateOrUpdateSkillsetAsync(skillset);
            logger.LogInternalInformation("Skillset created/updated successfully");

            // Create or update indexer
            logger.LogInternalInformation("Creating/updating indexer...");
            var indexer = new SearchIndexer(aiSearchIndexerName, dataSource.Name, index.Name)
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
            await indexerClient.CreateOrUpdateIndexerAsync(indexer);
            logger.LogInternalInformation("Indexer created/updated successfully");
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to create or update Azure AI Search indexer infrastructure");
            throw;
        }
    }


    internal SearchIndex GetDocumentIndex()
    {
        const string vectorSearchHnswProfile = "hnswProfile";
        const string vectorSearchExhasutiveKnnProfile = "exhaustiveKnnProfile";
        const string vectorSearchHnswConfig = "hnswConfig";
        const string vectorSearchExhaustiveKnnConfig = "exhaustiveKnn";
        const string vectorSearchVectorizer = "openAIVectorizer";
        const int modelDimensions = 1536;

        SearchIndex searchIndex = new(aiSearchIndexName)
        {
            VectorSearch = new()
            {
                Profiles =
                {
                    new VectorSearchProfile(vectorSearchHnswProfile, vectorSearchHnswConfig)
                    {
                        VectorizerName = vectorSearchVectorizer
                    },
                    new VectorSearchProfile(vectorSearchExhasutiveKnnProfile, vectorSearchExhaustiveKnnConfig)
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(vectorSearchHnswConfig),
                    new ExhaustiveKnnAlgorithmConfiguration(vectorSearchExhaustiveKnnConfig)
                },
                Vectorizers =
                {
                    new AzureOpenAIVectorizer(vectorSearchVectorizer)
                    {
                        Parameters = new AzureOpenAIVectorizerParameters()
                        {
                            ResourceUri = new Uri(openAISettings.Endpoint),
                            DeploymentName = openAISettings.EmbeddingGeneratorDeploymentName,
                            ModelName = openAISettings.EmbeddingGeneratorModelName,
                            AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(agentMemorySettings.ManagedIdentityResourceId))
                        }
                    }
                }
            },
            SemanticSearch = new()
            {
                Configurations =
                {
                    new SemanticConfiguration(semanticSearchConfig, new()
                    {
                        TitleField = new SemanticField(fieldName: "title"),
                        ContentFields =
                        {
                            new SemanticField(fieldName: "chunk")
                        },
                    })
                },
            },
            Fields =
            {
                new SearchableField("parent_id") { IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchableField("chunk_id") { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true, AnalyzerName = LexicalAnalyzerName.Keyword },
                new SearchableField("title"),
                new SearchableField("chunk"),
                new SearchField("vector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = modelDimensions,
                    VectorSearchProfileName = vectorSearchHnswProfile
                },
                new SearchableField("category") { IsFilterable = true, IsSortable = true, IsFacetable = true },
            },
        };

        return searchIndex;
    }

    public async Task<IList<SearchDocumentResult>> SearchCustomerDocumentsAsync(string query, uint k = 5, float? vectorSimilarityThreshold = null, bool exhaustiveKnn = false, string? filter = null, bool enableHybridSearch = false, CancellationToken cancellationToken = default)
    {
        var searchOptions = new SearchOptions
        {
            Filter = filter,
            Size = (int)Math.Min(k, maxK),
            Select = { "title", "chunk_id", "chunk", "parent_id" },
            IncludeTotalCount = true,
            VectorSearch = new VectorSearchOptions(),
        };

        searchOptions.VectorSearch.Queries.Add(new VectorizableTextQuery(query)
        {
            KNearestNeighborsCount = (int)Math.Min(k, maxK),
            Exhaustive = exhaustiveKnn,
            Fields = { "vector" },
            Threshold = vectorSimilarityThreshold.HasValue && vectorSimilarityThreshold.Value >= -1 && vectorSimilarityThreshold.Value <= 1
                ? new VectorSimilarityThreshold(vectorSimilarityThreshold.Value)
                : null,
        });

        searchOptions.QueryType = SearchQueryType.Semantic;
        searchOptions.SemanticSearch = new SemanticSearchOptions
        {
            SemanticConfigurationName = semanticSearchConfig,
            // QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
            // QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive)
        };


        var response = await searchClient.SearchAsync<SearchDocumentResult>(
                searchText: enableHybridSearch ? query : "*",
                options: searchOptions,
                cancellationToken: cancellationToken);
        var results = new List<SearchDocumentResult>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            logger.LogInternalInformation($"Found document: {result.Document.Title} with chunk_id: {result.Document.ChunkId}, parent_id: {result.Document.ParentId}, score: {result.Document.SearchScore}, reranker score: {result.Document.RerankerScore}");
            results.Add(result.Document);
        }

        return results;
    }
}
