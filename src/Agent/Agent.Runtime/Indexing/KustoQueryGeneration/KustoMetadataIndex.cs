// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Clients.Search;
using Agent.Core.Configuration;
using Azure.Core;
using Azure.Search.Documents.Indexes.Models;

namespace Agent.Runtime.Indexing.KustoQueryGeneration;

public class KustoMetadataIndex
{
    private readonly ISearchIndexingClient _searchIndexingClient;
    private readonly IndexingSettings _indexingSettings;
    private readonly OpenAISettings _openAiSettings;

    public KustoMetadataIndex(
        ISearchIndexingClient searchIndexingClient,
        IndexingSettings indexingSettings,
        OpenAISettings openAiSettings)
    {
        _searchIndexingClient = searchIndexingClient;
        _indexingSettings = indexingSettings;
        _openAiSettings = openAiSettings;
    }

    public async Task CreateOrUpdateIndex<T>(
        string indexName,
        string blobContainer,
        string blobRootPath = "",
        int vectorDimensions = 1536)
    {
        string blobStorageResourceId = _indexingSettings.BlobStorageResourceId;
        string managedIdentityResourceId = _indexingSettings.ManagedIdentityResourceId;
        string openAiEndpoint = _openAiSettings.Endpoint;
        string openAiEmbeddingsDeployment = _openAiSettings.EmbeddingGeneratorDeploymentName;
        string openAiEmbeddingsModel = _openAiSettings.EmbeddingGeneratorModelName;

        var builder = new Azure.Search.Documents.Indexes.FieldBuilder();
        var fields = (List<SearchField>)builder.Build(typeof(T));

        // add extra fields for chunked data and fetching first chunked vector 
        const string mergedVectorFieldName = "MetadataConcat_vector";
        const string vectorChunkFieldName = "MetadataConcatChunks";

        if (!fields.Any(f => f.Name == mergedVectorFieldName))
            fields.Add(new VectorSearchField(
                mergedVectorFieldName,
                vectorDimensions,
                $"{indexName}-azureOpenAi-profile"
            ));

        var skillsetName = $"{indexName}-embedding-skillset";

        // we first split our concatenated metadata into chunks
        var splitSkill = new SplitSkill(
            inputs: new[]
            {
                new InputFieldMappingEntry("text") { Source = "/document/MetadataConcat" }
            },
            outputs: new[]
            {
                new OutputFieldMappingEntry("textItems") { TargetName = vectorChunkFieldName }
            }
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
        var embeddingSkill = new AzureOpenAIEmbeddingSkill(
            inputs: new[]
            {
                new InputFieldMappingEntry("text") { Source = $"/document/{vectorChunkFieldName}/*" }
            },
            outputs: new[]
            {
                new OutputFieldMappingEntry("embedding") { TargetName = "MetadataConcat_vector_chunks" }
            }
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

        var skillset = new SearchIndexerSkillset(skillsetName, new List<SearchIndexerSkill> { splitSkill, embeddingSkill })
        {
            Description = $"Skillset for {indexName} metadata embedding"
        };
        await _searchIndexingClient.CreateOrUpdateSkillsetAsync(skillset);

        var hnsw = new HnswAlgorithmConfiguration($"{indexName}-hnsw")
        {
            Parameters = new HnswParameters
            {
                Metric = VectorSearchAlgorithmMetric.Cosine,
                M = 4,
                EfConstruction = 400,
                EfSearch = 500
            }
        };

        var vectorProfile = new VectorSearchProfile($"{indexName}-azureOpenAi-profile", $"{indexName}-hnsw");
        var vectorSearch = new VectorSearch
        {
            Algorithms = { hnsw },
            Profiles = { vectorProfile }
        };

        var index = new SearchIndex(indexName, fields)
        {
            VectorSearch = vectorSearch
        };
        await _searchIndexingClient.CreateOrUpdateIndexAsync(index, recreateOnError: true);

        var dataSourceName = $"{indexName}-blob-datasource";
        await _searchIndexingClient.CreateOrUpdateBlobDataSourceAsync(
            dataSourceName,
            blobContainer,
            blobRootPath,
            new ResourceIdentifier(blobStorageResourceId),
            new ResourceIdentifier(managedIdentityResourceId)
        );

        var indexerName = $"{indexName}-indexer";
        var indexer = new SearchIndexer(indexerName, dataSourceName, indexName)
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
            new FieldMapping($"/document/{vectorChunkFieldName}/0/MetadataConcat_vector_chunks")
            {
                TargetFieldName = "MetadataConcat_vector"
            }
        );

        await _searchIndexingClient.CreateOrUpdateIndexerAsync(indexer);
    }
}
