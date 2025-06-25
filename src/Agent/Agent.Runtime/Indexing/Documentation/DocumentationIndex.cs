// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Clients.Search;
using Agent.Core.Configuration;
using Azure.Core;
using Azure.Search.Documents.Indexes.Models;

namespace Agent.Runtime.Indexing.Documentation;

public class DocumentationIndex
{
    private readonly ISearchIndexingClient _searchIndexingClient;
    private readonly IndexingSettings _indexingSettings;
    private readonly OpenAISettings _openAiSettings;

    public DocumentationIndex(ISearchIndexingClient searchIndexingClient, IndexingSettings indexingSettings, OpenAISettings openAiSettings)
    {
        _searchIndexingClient = searchIndexingClient;
        _indexingSettings = indexingSettings;
        _openAiSettings = openAiSettings;
    }

    public async Task CreateSearchIndexer(string indexName, string blobContainer, string blobRootPath)
    {
        string dataSourceName = $"{indexName}-blob-datasource";
        string indexerName = $"{indexName}-indexer";
        string skillsetName = $"{indexName}-skillset";
            
        SearchIndex index = CreateSearchIndex(indexName, _openAiSettings.Endpoint, new ResourceIdentifier(_indexingSettings.ManagedIdentityResourceId));
        SearchIndexerSkillset skillset = CreateSearchIndexSkillset(skillsetName, _openAiSettings.Endpoint, new ResourceIdentifier(_indexingSettings.ManagedIdentityResourceId), indexName);
        SearchIndexer indexer = new SearchIndexer(indexerName, dataSourceName, indexName);

        indexer.Parameters = new IndexingParameters()
        {
            IndexingParametersConfiguration = new IndexingParametersConfiguration()
            {
                DataToExtract = BlobIndexerDataToExtract.ContentAndMetadata,
                ParsingMode = BlobIndexerParsingMode.Markdown,
                MarkdownHeaderDepth = MarkdownHeaderDepth.H3,
                MarkdownParsingSubmode = MarkdownParsingSubmode.OneToMany,
                FailOnUnsupportedContentType = true
            }
        };

        indexer.FieldMappings.Add(new FieldMapping("metadata_storage_name") {  TargetFieldName = "title" });

        await _searchIndexingClient.CreateOrUpdateBlobDataSourceAsync(dataSourceName, blobContainer, blobRootPath, new ResourceIdentifier(_indexingSettings.BlobStorageResourceId), new ResourceIdentifier(_indexingSettings.ManagedIdentityResourceId));
        await _searchIndexingClient.CreateOrUpdateIndexAsync(index, recreateOnError: true);
        await _searchIndexingClient.CreateOrUpdateSkillsetAsync(skillset);
        await _searchIndexingClient.CreateOrUpdateIndexerAsync(indexer);
    }

    private SearchIndexerSkillset CreateSearchIndexSkillset(string skillsetName, string openAiEndpoint, ResourceIdentifier userAssignedIdentity, string indexName)
    {
        // Create the text splitting skill
        SplitSkill splitSkill = new SplitSkill(
            inputs: new List<InputFieldMappingEntry>
            {
                new InputFieldMappingEntry("text")
                {
                    Source = "/document/content"
                }
            },
            outputs: new List<OutputFieldMappingEntry>
            {
                new OutputFieldMappingEntry("textItems")
                {
                    TargetName = "pages"
                }
            })
        {
            Name = "TextSplitter",
            Context = "/document",
            DefaultLanguageCode = "en",
            TextSplitMode = TextSplitMode.Pages,
            MaximumPageLength = 2000,
            PageOverlapLength = 500,
            MaximumPagesToTake = 0,
            Description = "Split skill to chunk documents"
        };

        // Create the OpenAI embedding skill
        AzureOpenAIEmbeddingSkill embeddingSkill = new AzureOpenAIEmbeddingSkill(
            inputs: new List<InputFieldMappingEntry>
            {
                new InputFieldMappingEntry("text")
                {
                    Source = "/document/pages/*"
                }
            },
            outputs: new List<OutputFieldMappingEntry>
            {
                new OutputFieldMappingEntry("embedding")
                {
                    TargetName = "text_vector"
                }
            })
        {
            Context = "/document/pages/*",
            ResourceUri = new Uri(openAiEndpoint),
            DeploymentName = "text-embedding-3-large",
            ModelName = "text-embedding-3-large",
            Dimensions = 3072,
            AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(userAssignedIdentity),
        };


        // Create mappings
        List<InputFieldMappingEntry> selectorMappings =
        [
            new InputFieldMappingEntry("text_vector")
            {
                Source = "/document/pages/*/text_vector"
            },
            new InputFieldMappingEntry("chunk")
            {
                Source = "/document/pages/*"
            },
            new InputFieldMappingEntry("title")
            {
                Source = "/document/title"
            },
            new InputFieldMappingEntry("header_1")
            {
                Source = "/document/sections/h1"
            },
            new InputFieldMappingEntry("header_2")
            {
                Source = "/document/sections/h2"
            },
            new InputFieldMappingEntry("header_3")
            {
                Source = "/document/sections/h3"
            }
        ];

        // Create index projections
        SearchIndexerIndexProjectionSelector selector = new SearchIndexerIndexProjectionSelector(
            targetIndexName: indexName,
            parentKeyFieldName: "parent_id",
            sourceContext: "/document/pages/*",
            mappings: selectorMappings);

        SearchIndexerIndexProjection indexProjection = new SearchIndexerIndexProjection([selector])
        {
            Parameters = new SearchIndexerIndexProjectionsParameters()
            {
                ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
            }
        };
                
        // Create the skillset
        SearchIndexerSkillset skillset = new SearchIndexerSkillset(skillsetName, [splitSkill, embeddingSkill])
        {
            Description = "Skillset to chunk documents and generate embeddings",
            IndexProjection = indexProjection
        };

        return skillset;
    }

    /// <summary>
    /// Creates a search index 
    /// </summary>
    /// <param name="indexName">Name of the index to create</param>
    /// <param name="openAiEndpoint">Azure OpenAI endpoint to use for vectorization</param>
    /// <returns>A configured SearchIndex object</returns>
    private SearchIndex CreateSearchIndex(string indexName, string openAiEndpoint, ResourceIdentifier userAssignedIdentity)
    {
        // Create the fields collection
        List<SearchField> fields = new List<SearchField>
        {
            // Key field
            new SearchableField("chunk_id")
            {
                IsKey = true,
                IsSortable = true,
                AnalyzerName = LexicalAnalyzerName.Keyword
            },
            
            // Parent ID field
            new SimpleField("parent_id", SearchFieldDataType.String)
            {
                IsFilterable = true
            },
            
            // Content fields
            new SearchableField("chunk"),
            new SearchableField("title"),
            new SearchableField("header_1"),
            new SearchableField("header_2"),
            new SearchableField("header_3"),

            // Vector field
            new VectorSearchField("text_vector", 3072,  $"{indexName}-azureOpenAi-text-profile"),
        };

        SemanticPrioritizedFields semanticPrioritizedFields = new SemanticPrioritizedFields();
        semanticPrioritizedFields.TitleField = new SemanticField("title");
        semanticPrioritizedFields.ContentFields.Add(new SemanticField("chunk"));

        // Create the semantic configuration
        SemanticConfiguration semanticConfig = new SemanticConfiguration($"{indexName}-semantic-configuration", semanticPrioritizedFields)
        {
            RankingOrder = RankingOrder.BoostedRerankerScore,
            FlightingOptIn = false
        };

        // Create the HNSW algorithm configuration for vector search
        HnswAlgorithmConfiguration hnswAlgorithm = new HnswAlgorithmConfiguration($"{indexName}-algorithm")
        {
            Parameters = new HnswParameters
            {
                Metric = VectorSearchAlgorithmMetric.Cosine,
                M = 4,
                EfConstruction = 400,
                EfSearch = 500
            }
        };

        // Create the vectorizer for Azure OpenAI
        AzureOpenAIVectorizer vectorizer = new AzureOpenAIVectorizer($"{indexName}-azureOpenAi-text-vectorizer")
        {
            Parameters = new AzureOpenAIVectorizerParameters
            {
                ResourceUri = new Uri(openAiEndpoint),
                DeploymentName = "text-embedding-3-large",
                ModelName = "text-embedding-3-large",
                AuthenticationIdentity = new SearchIndexerDataUserAssignedIdentity(userAssignedIdentity)
            }
        };

        // Create the vector profile
        VectorSearchProfile vectorProfile = new VectorSearchProfile(
            $"{indexName}-azureOpenAi-text-profile",
            $"{indexName}-algorithm");

        vectorProfile.VectorizerName = vectorizer.VectorizerName;

        // Create the search index
        SearchIndex searchIndex = new SearchIndex(indexName, fields)
        {
            Similarity = new BM25Similarity(),
            SemanticSearch = new SemanticSearch
            {
                DefaultConfigurationName = semanticConfig.Name,
                Configurations = { semanticConfig }
            },
            VectorSearch = new VectorSearch
            {
                Algorithms = { hnswAlgorithm },
                Profiles = { vectorProfile },
                Vectorizers = { vectorizer }
            }
        };

        return searchIndex;
    }
}
