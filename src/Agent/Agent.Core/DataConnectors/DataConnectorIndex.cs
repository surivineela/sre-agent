// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Clients.Search;
using Agent.Core.Clients.Storage;
using Agent.Core.Configuration;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Core.DataConnectors;

public class DataConnectorIndex
{
    private readonly ISearchIndexingClient _searchIndexingClient;
    private readonly IndexingSettings _indexingSettings;
    private readonly OpenAISettings _openAiSettings;
    private readonly List<SearchField> _fields;
    private readonly DataConnectorSearchSettings _searchSettings;
    private readonly DataConnectorStorageSettings _storageSettings;
    private readonly IAzureBlobStorageClient _azureBlobStorageClient;
    private readonly ILogger<DataConnectorIndex> _logger;

    public SearchField? SemanticSearchTitleField
    {
        get;
    }

    public IReadOnlyList<SearchField> SemanticSearchContentFields
    {
        get;
    }

    public DataConnectorIndex(
        ISearchIndexingClient searchIndexingClient,
        IAzureBlobStorageClient azureBlobStorageClient,
        IndexingSettings indexingSettings,
        OpenAISettings openAiSettings,
        IOptions<DataConnectorSettings> dataConnectorSettings,
        ILogger<DataConnectorIndex> logger)
    {
        _azureBlobStorageClient = azureBlobStorageClient;
        _searchIndexingClient = searchIndexingClient;
        _indexingSettings = indexingSettings;
        _openAiSettings = openAiSettings;
        _logger = logger;

        FieldBuilder builder = new FieldBuilder();

        _fields = (List<SearchField>)builder.Build(typeof(DataConnectorIndexDocument));
        List<SearchField> contentFields = new List<SearchField>();

        // Inspect DataConnectorIndexDocument properties for semantic search attributes
        foreach (PropertyInfo property in typeof(DataConnectorIndexDocument).GetProperties())
        {
            object? semanticAttribute = property.GetCustomAttributes(typeof(SemanticSearchAttribute), false).FirstOrDefault();
            if (semanticAttribute != null)
            {
                SearchField? searchField = _fields.FirstOrDefault(f => f.Name == property.Name);
                if (searchField != null)
                {
                    if (semanticAttribute is SemanticSearchAttribute { FieldType: SemanticSearchFieldType.TitleField })
                    {
                        SemanticSearchTitleField = searchField;
                    }
                    else if (semanticAttribute is SemanticSearchAttribute { FieldType: SemanticSearchFieldType.ContentField })
                    {
                        contentFields.Add(searchField);
                    }
                }
            }
        }

        SemanticSearchContentFields = contentFields.AsReadOnly();
        _searchSettings = dataConnectorSettings.Value.Search;
        _storageSettings = dataConnectorSettings.Value.Storage;
    }

    public async Task CreateOrUpdateIndex()
    {
        string blobStorageResourceId = _indexingSettings.BlobStorageResourceId;
        string managedIdentityResourceId = _indexingSettings.ManagedIdentityResourceId;
        string openAiEndpoint = _openAiSettings.Endpoint;
        string openAiEmbeddingsDeployment = _openAiSettings.EmbeddingGeneratorDeploymentName;
        string openAiEmbeddingsModel = _openAiSettings.EmbeddingGeneratorModelName;

        string blobContainer = _storageSettings.BlobStorageContainerName;
        string indexName = _searchSettings.IndexName;
        string skillsetName = _searchSettings.SkillsetName;
        string indexerName = _searchSettings.IndexerName;
        string dataSourceName = _searchSettings.DataSourceName;

        // add extra fields for chunked data and fetching first chunked vector
        const string documentContext = "/document";
        const string chunkFieldName = "pages";
        const string vectorChunkOutputFieldName = "vector_chunks";

        SearchField vectorField = _fields.Single(x => x.Name == nameof(DataConnectorIndexDocument.Vector));
        SearchField parentField = _fields.Single(x => x.Name == nameof(DataConnectorIndexDocument.ParentId));

        // we first split our concatenated metadata into chunks
        SplitSkill splitSkill = new SplitSkill(
            inputs:
            [
                new InputFieldMappingEntry("text") { Source = $"{documentContext}/{nameof(DataConnectorSourceDocument.Contents)}" }
            ],
            outputs:
            [
                new OutputFieldMappingEntry("textItems") { TargetName = chunkFieldName }
            ]
        )
        {
            Description = "Splits Content into chunks for embedding",
            Context = documentContext,
            DefaultLanguageCode = "en",
            TextSplitMode = TextSplitMode.Pages,
            MaximumPageLength = 4000,
            PageOverlapLength = 0,
            MaximumPagesToTake = 0,
            Unit = SplitSkillUnit.Characters
        };

        // then we embed each chunk of metadata and generate vector embeddings for each chunk
        AzureOpenAIEmbeddingSkill embeddingSkill = new AzureOpenAIEmbeddingSkill(
            inputs:
            [
                new InputFieldMappingEntry("text") { Source = $"{documentContext}/{chunkFieldName}/*" }
            ],
            outputs:
            [
                new OutputFieldMappingEntry("embedding") { TargetName = vectorChunkOutputFieldName }
            ]
        )
        {
            Name = "embeddingskill",
            Description = "Embeds each chunk of text",
            Context = $"{documentContext}/{chunkFieldName}/*",
            ResourceUri = new Uri(openAiEndpoint),
            DeploymentName = openAiEmbeddingsDeployment,
            ModelName = openAiEmbeddingsModel,
            Dimensions = vectorField.VectorSearchDimensions,
            AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(managedIdentityResourceId))
        };

        List<InputFieldMappingEntry> selectorMappings =
        [
            new InputFieldMappingEntry(nameof(DataConnectorIndexDocument.Vector))
            {
                Source = $"{documentContext}/{chunkFieldName}/*/{vectorChunkOutputFieldName}"
            },
            new InputFieldMappingEntry(nameof(DataConnectorIndexDocument.Chunk))
            {
                Source = $"{documentContext}/{chunkFieldName}/*"
            },
            new InputFieldMappingEntry(nameof(DataConnectorIndexDocument.Title))
            {
                Source = $"{documentContext}/{nameof(DataConnectorSourceDocument.Title)}"
            },
            new InputFieldMappingEntry(nameof(DataConnectorIndexDocument.Type))
            {
                Source = $"{documentContext}/{nameof(DataConnectorSourceDocument.Type)}"
            },
            new InputFieldMappingEntry(nameof(DataConnectorIndexDocument.Filter))
            {
                Source = $"{documentContext}/{nameof(DataConnectorSourceDocument.Filter)}"
            },
            new InputFieldMappingEntry(nameof(DataConnectorIndexDocument.SourceDocumentUrl))
            {
                Source = $"{documentContext}/metadata_storage_path"
            },
        ];

        // Create index projections
        SearchIndexerIndexProjectionSelector selector = new SearchIndexerIndexProjectionSelector(
            targetIndexName: indexName,
            parentKeyFieldName: parentField.Name,
            sourceContext: $"{documentContext}/{chunkFieldName}/*",
            mappings: selectorMappings);

        SearchIndexerIndexProjection indexProjection = new SearchIndexerIndexProjection([selector])
        {
            Parameters = new SearchIndexerIndexProjectionsParameters()
            {
                ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
            }
        };

        SearchIndexerSkillset skillset = new SearchIndexerSkillset(skillsetName, new List<SearchIndexerSkill> { splitSkill, embeddingSkill })
        {
            Description = $"Skillset for {indexName} metadata embedding",
            IndexProjection = indexProjection,
        };

        HnswAlgorithmConfiguration hnsw = new HnswAlgorithmConfiguration($"{indexName}-hnsw")
        {
            Parameters = new HnswParameters
            {
                Metric = VectorSearchAlgorithmMetric.Cosine,
                M = 4,
                EfConstruction = 400,
                EfSearch = 500
            }
        };

        // Create vectorizer for integrated vectorization at query time
        AzureOpenAIVectorizer vectorizer = new AzureOpenAIVectorizer($"{indexName}-vectorizer")
        {
            Parameters = new AzureOpenAIVectorizerParameters
            {
                ResourceUri = new Uri(openAiEndpoint),
                DeploymentName = openAiEmbeddingsDeployment,
                ModelName = openAiEmbeddingsModel,
                AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(managedIdentityResourceId))
            }
        };

        VectorSearchProfile vectorProfile = new VectorSearchProfile(vectorField.VectorSearchProfileName, hnsw.Name)
        {
            VectorizerName = vectorizer.VectorizerName
        };

        VectorSearch vectorSearch = new VectorSearch
        {
            Algorithms = { hnsw },
            Profiles = { vectorProfile },
            Vectorizers = { vectorizer }
        };

        // Configure semantic search if type has semantic fields
        SemanticSearch? semanticSearch = null;
        if (SemanticSearchTitleField != null)
        {
            SemanticConfiguration semanticConfiguration = new SemanticConfiguration($"{indexName}-semantic-config", new SemanticPrioritizedFields()
            {
                TitleField = new SemanticField(SemanticSearchTitleField.Name)
            });

            foreach (SemanticField? item in SemanticSearchContentFields.Select(f => new SemanticField(f.Name)))
            {
                semanticConfiguration.PrioritizedFields.ContentFields.Add(item);
            }

            semanticSearch = new SemanticSearch()
            {
                Configurations = { semanticConfiguration }
            };
        }

        SearchIndex index = new SearchIndex(indexName, _fields)
        {
            VectorSearch = vectorSearch,
            SemanticSearch = semanticSearch
        };

        SearchIndexer indexer = new SearchIndexer(indexerName, dataSourceName, indexName)
        {
            SkillsetName = skillsetName,
            Schedule = new IndexingSchedule(TimeSpan.FromHours(1)),
            Parameters = new IndexingParameters
            {
                IndexingParametersConfiguration = new IndexingParametersConfiguration
                {
                    DataToExtract = BlobIndexerDataToExtract.ContentAndMetadata,
                    ParsingMode = BlobIndexerParsingMode.Json
                }
            }
        };

        indexer.FieldMappings.Add(new FieldMapping("metadata_storage_path")
        {
            TargetFieldName = nameof(DataConnectorIndexDocument.SourceDocumentUrl)
        });

        await _searchIndexingClient.CreateOrUpdateIndexAsync(index);
        await _searchIndexingClient.CreateOrUpdateSkillsetAsync(skillset);
        await _searchIndexingClient.CreateOrUpdateBlobDataSourceAsync(
            name: dataSourceName,
            containerName: blobContainer,
            rootPath: string.Empty,
            blobStorageResourceId: new ResourceIdentifier(blobStorageResourceId),
            managedIdentityResourceId: new ResourceIdentifier(managedIdentityResourceId)
        );

        await _searchIndexingClient.CreateOrUpdateIndexerAsync(indexer);
    }

    public async Task RunIndexerAsync(CancellationToken cancellationToken = default)
    {
        await _searchIndexingClient.RunIndexerAsync(_searchSettings.IndexerName, cancellationToken);
    }

    public async IAsyncEnumerable<DataConnectorSearchResult<T>> SearchAsync<T>(string query, string filter, int max, float? vectorSimilarityThreshold = null) where T : DataConnectorSourceDocument
    {
        var vectorQuery = new VectorizableTextQuery(query)
        {
            Fields = { nameof(DataConnectorIndexDocument.Vector) },
            KNearestNeighborsCount = max,
        };

        // Apply vector similarity threshold if provided (must be between -1 and 1 for Cosine similarity)
        if (vectorSimilarityThreshold.HasValue && vectorSimilarityThreshold.Value >= -1 && vectorSimilarityThreshold.Value <= 1)
        {
            vectorQuery.Threshold = new VectorSimilarityThreshold(vectorSimilarityThreshold.Value);
        }

        SearchOptions searchOptions = new SearchOptions()
        {
            QueryType = SemanticSearchTitleField != null ? SearchQueryType.Semantic : SearchQueryType.Simple,
            Size = max,
            Filter = $"Type eq '{typeof(T).Name}' " + (string.IsNullOrEmpty(filter) ? string.Empty : $"and Filter eq '{filter}'"),
            VectorSearch = new VectorSearchOptions()
            {
                Queries = { vectorQuery }
            }
        };

        // Enable semantic search if configured
        if (SemanticSearchTitleField != null)
        {
            searchOptions.SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = $"{_searchSettings.IndexName}-semantic-config",
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive)
            };
        }

        SearchResults<DataConnectorIndexDocument> searchResults = await _searchIndexingClient.SearchAsync<DataConnectorIndexDocument>(
            indexName: _searchSettings.IndexName,
            searchText: query,
            searchOptions: searchOptions);

        Azure.AsyncPageable<SearchResult<DataConnectorIndexDocument>> results = searchResults.GetResultsAsync();

        List<DataConnectorIndexDocument> orphanedDocuments = new List<DataConnectorIndexDocument>();

        await foreach (SearchResult<DataConnectorIndexDocument> result in results)
        {
            // Try to get the original document, and track orphaned documents for cleanup
            var (originalDocument, isOrphaned) = await TryGetOriginalDocumentAsync<T>(result.Document);

            if (isOrphaned)
            {
                // Track orphaned document for batch cleanup
                orphanedDocuments.Add(result.Document);
                _logger.LogInternalWarning(
                    "Orphaned document detected in search index. DocumentId: {DocumentId}, SourceUrl: {SourceUrl}. Will be removed from index.",
                    result.Document.id,
                    result.Document.SourceDocumentUrl);
                continue; // Skip orphaned documents - don't yield them
            }

            if (originalDocument != null)
            {
                yield return new DataConnectorSearchResult<T>
                {
                    SearchResult = result,
                    OriginalDocument = originalDocument
                };
            }
        }

        // Clean up orphaned documents in batch after iteration
        if (orphanedDocuments.Count > 0)
        {
            await CleanupOrphanedDocumentsAsync(orphanedDocuments);
        }
    }

    private async Task CleanupOrphanedDocumentsAsync(List<DataConnectorIndexDocument> orphanedDocuments)
    {
        try
        {
            _logger.LogInternalWarning(
                "Removing {Count} orphaned documents from search index {IndexName}",
                orphanedDocuments.Count,
                _searchSettings.IndexName);

            var documentIds = orphanedDocuments
                .Where(d => !string.IsNullOrEmpty(d.id))
                .Select(d => d.id)
                .ToHashSet();

            await _searchIndexingClient.DeleteDocumentsAsync(
                _searchSettings.IndexName,
                documentIds);

            _logger.LogInternalInformation(
                "Successfully removed {Count} orphaned documents from search index {IndexName}",
                orphanedDocuments.Count,
                _searchSettings.IndexName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "Failed to remove {Count} orphaned documents from search index {IndexName}. Documents: {DocumentIds}",
                orphanedDocuments.Count,
                _searchSettings.IndexName,
                string.Join(", ", orphanedDocuments.Select(d => d.id)));
        }
    }

    private async Task<(T? document, bool isOrphaned)> TryGetOriginalDocumentAsync<T>(DataConnectorIndexDocument indexDocument)
    {
        try
        {
            Stream stream = await _azureBlobStorageClient.DownloadBlobContentsAsStreamAsync(new Uri(indexDocument.SourceDocumentUrl));

            T? document = JsonSerializer.Deserialize<T>(stream);
            if (document == null)
            {
                _logger.LogInternalError(
                    "Failed to deserialize document from {SourceUrl}. DocumentId: {DocumentId}",
                    indexDocument.SourceDocumentUrl,
                    indexDocument.id);
                return (default, false);
            }

            return (document, false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Document blob no longer exists - this is an orphaned index entry
            _logger.LogInternalWarning(
                "Blob not found for index document. DocumentId: {DocumentId}, SourceUrl: {SourceUrl}, ErrorCode: {ErrorCode}",
                indexDocument.id,
                indexDocument.SourceDocumentUrl,
                ex.ErrorCode);
            return (default, true);
        }
        catch (Exception ex)
        {
            // Other errors (network issues, auth failures, etc.) should not be treated as orphaned documents
            _logger.LogInternalError(
                ex,
                "Error retrieving original document from {SourceUrl}. DocumentId: {DocumentId}. Error: {ErrorMessage}",
                indexDocument.SourceDocumentUrl,
                indexDocument.id,
                ex.Message);
            return (default, false);
        }
    }

    public async Task<T> GetOriginalDocumentAsync<T>(DataConnectorIndexDocument indexDocument)
    {
        try
        {
            Stream stream = await _azureBlobStorageClient.DownloadBlobContentsAsStreamAsync(new Uri(indexDocument.SourceDocumentUrl));

            return JsonSerializer.Deserialize<T>(stream)
                ?? throw new InvalidOperationException($"Failed to deserialize document from {indexDocument.SourceDocumentUrl}");
        }
        catch (Exception ex)
        {
            // TODO: debug why TsgDocumentMetadata does not have all the required fields (that are now set to optional to unblock) being present in the object when the data is being pulled from blob/AI Search
            throw new InvalidOperationException($"Failed to get original document from URL: {indexDocument.SourceDocumentUrl}. Error: {ex.Message}", ex);
        }
    }
}
