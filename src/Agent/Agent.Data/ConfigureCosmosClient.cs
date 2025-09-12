// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Data;

public static class AgentDataConfiguration
{
    public const string ThreadContainerName = "documents";
    public const string AgentContextContainerName = "agentContexts";
    public const string InstanceManagementContainerName = "instanceManagement";
    public const string InstanceAssignmentsContainerName = "instanceAssignments";
    public const string LeaseContainerName = "changeFeedLeases";
    public const string ReasoningLoopContainerName = "reasoningloopdocs";
    public const string ExtendedAgentContainerName = "extendedagents";
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
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
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

        // Add Thread Orchestration Mapping repository registration
        serviceCollection.AddSingleton<IThreadOrchestrationMappingRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;
            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();

            return new CosmosDbThreadOrchestrationMappingRepository(
                cosmosClient,
                cosmosDatabaseName);
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

        // Add Thread Management repository registration
        serviceCollection.AddSingleton<IInstanceManagementRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;
            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbInstanceManagementRepository>>();
            var threadManagementSettings = serviceProvider.GetRequiredService<InstanceManagementSettings>();

            return new CosmosDbInstanceManagementRepository(
                cosmosClient,
                cosmosDatabaseName,
                logger,
                threadManagementSettings
            );
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

        return serviceCollection;
    }

    public static async Task CreateCosmosContainerIfNotExists(this IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();

        var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
        var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();

        var cosmosDatabaseName = cosmosDbSettings.Docs.Database;
        // Ensure database exists
        DatabaseResponse database = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosDatabaseName);

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
            id: LeaseContainerName,
            partitionKeyPath: "/id", // change feed leases must be partitioned by ID
            throughput: null // Use the database level shared RU.
        );

        await database.Database.CreateContainerIfNotExistsAsync(
            id: InstanceManagementContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: null // Use the database level shared RU first.
        );

        await database.Database.CreateContainerIfNotExistsAsync(
            id: InstanceAssignmentsContainerName,
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

        // The encryption key should be created from control plane because agent MI does not have permission
        //var encryptionPath = new ClientEncryptionIncludedPath
        //{
        //    Path = ReasoningLoopDocumentEncryptedPath,
        //    ClientEncryptionKeyId = ReasoningLoopEncryptionKeyName,
        //    EncryptionType = EncryptionType.Deterministic,
        //    EncryptionAlgorithm = DataEncryptionAlgorithm.AeadAes256CbcHmacSha256
        //};

        //await database.Database.DefineContainer(ReasoningLoopContainerName, "/partitionKey")
        //    .WithClientEncryptionPolicy()
        //    .WithIncludedPath(encryptionPath)
        //    .Attach()
        //    .CreateIfNotExistsAsync();
        // Commented out the vector index creation for now. Leave it here for future reference.
        // var embeddings = new List<Embedding>
        // {
        //     new()
        //     {
        //         DataType = VectorDataType.Float32,
        //         Dimensions = 1536, // Set the vector size to 1536 for OpenAI embeddings
        //         DistanceFunction = DistanceFunction.Cosine,
        //         Path = "/descriptionVector"
        //     },
        //     new()
        //     {
        //         DataType = VectorDataType.Float32,
        //         Dimensions = 1536, // Set the vector size to 1536 for OpenAI embeddings
        //         DistanceFunction = DistanceFunction.Cosine,
        //         Path = "/titleVector"
        //     }
        // };

        // var collection = new Collection<Embedding>(embeddings);
        // // see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-dotnet-vector-index-query#create-a-vector-index-in-the-indexing-policy
        // var properties = new ContainerProperties
        // {
        //     Id = IncidentContainerName,
        //     PartitionKeyPath = "/partitionKey",
        //     DefaultTimeToLive = -1, // Set to -1 to disable TTL
        //     VectorEmbeddingPolicy = new(collection),
        //     IndexingPolicy = new IndexingPolicy
        //     {
        //         VectorIndexes =
        //         [
        //             new VectorIndexPath()
        //             {
        //                 Path = "/descriptionVector",
        //                 Type = VectorIndexType.DiskANN,
        //             },
        //             new VectorIndexPath()
        //             {
        //                 Path = "/titleVector",
        //                 Type = VectorIndexType.QuantizedFlat, // DiskANN index has a limit of 1 per container. Use QuantizedFlat instead
        //             }
        //         ]
        //     }
        // };

        // properties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
        // properties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/descriptionVector/*" });
        // properties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/titleVector/*" });
        // await database.Database.CreateContainerIfNotExistsAsync(
        //     properties,
        //     throughput: 1000 // Minimum throughput for now
        // );

    }
}

