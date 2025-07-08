// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Attributes;
using Agent.Core.Clients.Search;
using Agent.Core.Configuration;
using Azure.Core;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace Agent.Plugins.DataConnectors.KustoMetadata;

public class KustoMetadataIndex<T>
{
    private readonly string _indexNamePrefix;
    private readonly ISearchIndexingClient _searchIndexingClient;
    private readonly IndexingSettings _indexingSettings;
    private readonly OpenAISettings _openAiSettings;

    public string VectorFieldName
    {
        get
        {
            return "MetadataConcat_vector";
        }
    }

    public string IndexName
    {
        get
        {
            return $"{_indexNamePrefix}-index";
        }
    }

    public SearchField? SemanticSearchTitleField
    {
        get;
    }

    public IReadOnlyList<SearchField> SemanticSearchContentFields
    {
        get;
    }

    public KustoMetadataIndex(
        ISearchIndexingClient searchIndexingClient,
        IndexingSettings indexingSettings,
        OpenAISettings openAiSettings)
    {
        _indexNamePrefix = typeof(T).Name.ToLowerInvariant();
        _searchIndexingClient = searchIndexingClient;
        _indexingSettings = indexingSettings;
        _openAiSettings = openAiSettings;

        FieldBuilder builder = new FieldBuilder();
        List<SearchField> fields = (List<SearchField>)builder.Build(typeof(T));
        List<SearchField> contentFields = new List<SearchField>();

        foreach (PropertyInfo property in typeof(T).GetProperties())
        {
            object? semanticAttribute = property.GetCustomAttributes(typeof(SemanticSearchAttribute), false).FirstOrDefault();
            if (semanticAttribute != null)
            {
                SearchField? searchField = fields.FirstOrDefault(f => f.Name == property.Name);
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
    }

    public async Task CreateOrUpdateIndex(
        string blobContainer,
        string blobRootPath = "",
        int vectorDimensions = 1536)
    {
        string blobStorageResourceId = _indexingSettings.BlobStorageResourceId;
        string managedIdentityResourceId = _indexingSettings.ManagedIdentityResourceId;
        string openAiEndpoint = _openAiSettings.Endpoint;
        string openAiEmbeddingsDeployment = _openAiSettings.EmbeddingGeneratorDeploymentName;
        string openAiEmbeddingsModel = _openAiSettings.EmbeddingGeneratorModelName;

        // add extra fields for chunked data and fetching first chunked vector 
        const string vectorChunkFieldName = "MetadataConcatChunks";
        const string vectorChunkOutputFieldName = "MetadataConcat_vector_chunks";

        string skillsetName = $"{_indexNamePrefix}-embedding-skillset";

        // we first split our concatenated metadata into chunks
        SplitSkill splitSkill = new SplitSkill(
            inputs:
            [
                new InputFieldMappingEntry("text") { Source = "/document/MetadataConcat" }
            ],
            outputs:
            [
                new OutputFieldMappingEntry("textItems") { TargetName = vectorChunkFieldName }
            ]
        )
        {
            Description = "Splits MetadataConcat into chunks for embedding",
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
                new InputFieldMappingEntry("text") { Source = $"/document/{vectorChunkFieldName}/*" }
            ],
            outputs:
            [
                new OutputFieldMappingEntry("embedding") { TargetName = vectorChunkOutputFieldName }
            ]
        )
        {
            Name = "metadata_concat_embedding",
            Description = "Embeds each chunk of MetadataConcat",
            Context = $"/document/{vectorChunkFieldName}/*",
            ResourceUri = new Uri(openAiEndpoint),
            DeploymentName = openAiEmbeddingsDeployment,
            ModelName = openAiEmbeddingsModel,
            Dimensions = vectorDimensions,
            AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(managedIdentityResourceId))
        };

        SearchIndexerSkillset skillset = new SearchIndexerSkillset(skillsetName, new List<SearchIndexerSkill> { splitSkill, embeddingSkill })
        {
            Description = $"Skillset for {_indexNamePrefix} metadata embedding"
        };

        await _searchIndexingClient.CreateOrUpdateSkillsetAsync(skillset);

        HnswAlgorithmConfiguration hnsw = new HnswAlgorithmConfiguration($"{_indexNamePrefix}-hnsw")
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
        AzureOpenAIVectorizer vectorizer = new AzureOpenAIVectorizer($"{_indexNamePrefix}-vectorizer")
        {
            Parameters = new AzureOpenAIVectorizerParameters
            {
                ResourceUri = new Uri(openAiEndpoint),
                DeploymentName = openAiEmbeddingsDeployment,
                ModelName = openAiEmbeddingsModel,
                AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(new ResourceIdentifier(managedIdentityResourceId))
            }
        };

        VectorSearchProfile vectorProfile = new VectorSearchProfile($"{_indexNamePrefix}-azureOpenAi-profile", hnsw.Name)
        {
            VectorizerName = vectorizer.VectorizerName
        };

        VectorSearch vectorSearch = new VectorSearch
        {
            Algorithms = { hnsw },
            Profiles = { vectorProfile },
            Vectorizers = { vectorizer }
        };


        FieldBuilder builder = new FieldBuilder();
        List<SearchField> fields = (List<SearchField>)builder.Build(typeof(T));

        if (!fields.Any(f => f.Name == VectorFieldName))
            fields.Add(new VectorSearchField(
                VectorFieldName,
                vectorDimensions,
                vectorProfile.Name
            ));

        // Configure semantic search if type has semantic fields
        SemanticSearch? semanticSearch = null;
        if (SemanticSearchTitleField != null)
        {
            SemanticConfiguration semanticConfiguration = new SemanticConfiguration($"{_indexNamePrefix}-semantic-config", new SemanticPrioritizedFields()
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

        SearchIndex index = new SearchIndex(IndexName, fields)
        {
            VectorSearch = vectorSearch,
            SemanticSearch = semanticSearch
        };

        await _searchIndexingClient.CreateOrUpdateIndexAsync(index, recreateOnError: true);

        string dataSourceName = $"{_indexNamePrefix}-blob-datasource";
        await _searchIndexingClient.CreateOrUpdateBlobDataSourceAsync(
            dataSourceName,
            blobContainer,
            blobRootPath,
            new ResourceIdentifier(blobStorageResourceId),
            new ResourceIdentifier(managedIdentityResourceId)
        );

        string indexerName = $"{_indexNamePrefix}-indexer";
        SearchIndexer indexer = new SearchIndexer(indexerName, dataSourceName, IndexName)
        {
            SkillsetName = skillsetName,
            Parameters = new IndexingParameters
            {
                IndexingParametersConfiguration = new IndexingParametersConfiguration
                {
                    DataToExtract = BlobIndexerDataToExtract.ContentAndMetadata,
                    ParsingMode = BlobIndexerParsingMode.Json
                }
            }
        };

        // add output field mappings to ensure we can fetch the first chunked vector, the good news here is that in the enriched data structure we have access to all vector chunks and performing vector queries in that vector space should be easy once we design where we want to store these vector arrays
        indexer.OutputFieldMappings.Add(
            new FieldMapping($"/document/{vectorChunkFieldName}/0/{vectorChunkOutputFieldName}")
            {
                TargetFieldName = VectorFieldName
            }
        );

        await _searchIndexingClient.CreateOrUpdateIndexerAsync(indexer);
    }
}
