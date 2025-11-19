// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.Json;
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
            return new CosmosDbSessionInsightRepository(cosmosClient, cosmosDatabaseName, logger);
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
            return new CosmosDbSessionInsightRepository(cosmosClient, cosmosDatabaseName, logger);
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
    }
}

