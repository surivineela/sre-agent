// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.ObjectModel;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.AgentMemory;
using Agent.Data.Json;
using Agent.Data.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Data;

public static class AgentDataConfiguration
{
    public const string ThreadContainerName = "documents";
    public const string AgentContextContainerName = "agentContexts";
    public const string ReasoningLoopContainerName = "reasoningloopdocs";
    public const string ExtendedAgentContainerName = "extendedagents";
    // to be cleaned up after migration
    public const string KnowledgeGraphContainerNameDeprecated = "knowledgeGraph";
    public const string KnowledgeGraphContainerName = "knowledgeGraphv2";
    public const string ReasoningLoopEncryptionKeyName = "reansoningloopkey";
    public const string ReasoningLoopDocumentEncryptedPath = "/encryptedProperties";

    public static IServiceCollection AddCosmosClient(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();

            var cosmosAccountName = cosmosDbSettings.Docs.AccountName;
            var domainSuffix = cosmosDbSettings.Docs.DomainSuffix;
            var endpoint = $"https://{cosmosAccountName}.{domainSuffix}";
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosOptions = new CosmosClientOptions
            {
                Serializer = new CosmosSystemTextJsonSerializer(),
            };

            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            var tokenCredential = authService.GetDocumentDbCredential();
            //var keyResolver = new KeyResolver(tokenCredential);

            return new CosmosClient(endpoint, tokenCredential, cosmosOptions);
            //.WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);
        });

        // Register the repository
        serviceCollection.AddSingleton<IThreadRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbThreadRepository>>();
            return new CosmosDbThreadRepository(cosmosClient, cosmosDatabaseName, logger);
        });

        // Register the Session Insight repository
        serviceCollection.AddSingleton<ISessionInsightRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbSessionInsightRepository>>();
            var searchIndexService = serviceProvider.GetRequiredService<ISearchIndexService>();
            var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            return new CosmosDbSessionInsightRepository(cosmosClient, cosmosDatabaseName, logger, searchIndexService, embeddingGenerator);
        });

        // Add Thread Teams Conversation Mapping repository registration
        serviceCollection.AddSingleton<IThreadTeamsMappingRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbThreadTeamsMappingRepository>>();
            return new CosmosDbThreadTeamsMappingRepository(cosmosClient, logger, cosmosDatabaseName);
        });

        // Register the Incident repository
        serviceCollection.AddSingleton<IIncidentRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbIncidentRepository>>();
            return new CosmosDbIncidentRepository(cosmosClient, cosmosDatabaseName, logger);
        });

        // Register the AppHealthHistory repository
        serviceCollection.AddSingleton<IAppHealthHistoryRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbAppHealthHistoryRepository>>();
            return new CosmosDbAppHealthHistoryRepository(cosmosClient, cosmosDatabaseName, logger);
        });

        // Register the Extended Agent repository
        serviceCollection.AddSingleton<IExtendedAgentRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbExtendedAgentRepository>>();
            return new CosmosDbExtendedAgentRepository(cosmosClient, cosmosDatabaseName, logger);
        });

        // Register the AgentTasks repository
        serviceCollection.AddSingleton<IAgentTasksRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbAgentTasksRepository>>();
            return new CosmosDbAgentTasksRepository(cosmosClient, cosmosDatabaseName, logger);
        });

        // Register the ScheduledTask repository
        serviceCollection.AddSingleton<IScheduledTaskRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbScheduledTaskRepository>>();
            return new CosmosDbScheduledTaskRepository(cosmosClient, cosmosDatabaseName, logger);
        });

        // Register the SessionInsight repository
        serviceCollection.AddSingleton<ISessionInsightRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbSessionInsightRepository>>();
            var searchIndexService = serviceProvider.GetRequiredService<ISearchIndexService>();
            var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            return new CosmosDbSessionInsightRepository(cosmosClient, cosmosDatabaseName, logger, searchIndexService, embeddingGenerator);
        });

        return serviceCollection;
    }

    public static async Task CreateCosmosContainerIfNotExists(this IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();

        var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
        var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();

        var cosmosDatabaseName = cosmosDbSettings.Docs.Database;
        // Ensure database exists
        var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosDatabaseName);

        // NOTE: The Cosmos Container creation behavior
        // If the database is created with provsioned throughput, the containers created with throughput = null will not have their own provisioned throughput.
        // If the database is created without provsioned throughput, the containers created with throughput = null will have throughput = 400.

        // Ensure container exists with appropriate partition key
        await database.Database.CreateContainerIfNotExistsAsync(
            id: ThreadContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: null // Use the database level shared RU first.
        );

        await database.Database.CreateContainerIfNotExistsAsync(
            id: AgentContextContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: null // Use the database level shared RU first.
        );

        await database.Database.CreateContainerIfNotExistsAsync(
            id: ExtendedAgentContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: null // Use the database level shared RU first.
        );

        // Create Knowledge Graph container with fulltext search enabled
        var knowledgeGraphProperties = new ContainerProperties(
            id: KnowledgeGraphContainerName,
            partitionKeyPath: "/partitionKey");

        // Create indexing policy
        var indexingPolicy = new IndexingPolicy
        {
            IndexingMode = IndexingMode.Consistent,
            Automatic = true
        };

        // Add included paths
        indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });

        // Add vector index
        indexingPolicy.VectorIndexes.Add(new VectorIndexPath
        {
            Path = "/vector",
            Type = VectorIndexType.DiskANN
        });

        // Add fulltext indexes
        indexingPolicy.FullTextIndexes.Add(new FullTextIndexPath { Path = "/name" });
        indexingPolicy.FullTextIndexes.Add(new FullTextIndexPath { Path = "/entityType" });
        indexingPolicy.FullTextIndexes.Add(new FullTextIndexPath { Path = "/content" });

        // Assign indexing policy
        knowledgeGraphProperties.IndexingPolicy = indexingPolicy;

        // Create fulltext policy
        var fullTextPolicy = new FullTextPolicy
        {
            DefaultLanguage = "en-US"
        };

        // Initialize FullTextPaths collection
        fullTextPolicy.FullTextPaths =
        [
            // Add fulltext paths
            new FullTextPath { Path = "/name", Language = "en-US" },
            new FullTextPath { Path = "/entityType", Language = "en-US" },
            new FullTextPath { Path = "/content", Language = "en-US" },
        ];

        // Assign fulltext policy
        knowledgeGraphProperties.FullTextPolicy = fullTextPolicy;

        // Create vector embedding policy
        var vectorEmbeddingPolicy = new VectorEmbeddingPolicy(
        [
            new Microsoft.Azure.Cosmos.Embedding
            {
                Path = "/vector",
                DataType = VectorDataType.Float32,
                Dimensions = 1536,
                DistanceFunction = DistanceFunction.Cosine
            }
        ]);

        // Assign vector embedding policy
        knowledgeGraphProperties.VectorEmbeddingPolicy = vectorEmbeddingPolicy;

        await database.Database.CreateContainerIfNotExistsAsync(
            containerProperties: knowledgeGraphProperties,
            throughput: null // Use the database level shared RU first.
        );
    }
}

