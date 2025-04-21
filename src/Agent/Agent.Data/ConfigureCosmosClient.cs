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

            return new CosmosClient(endpoint, tokenCredential, cosmosOptions);
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

        // Ensure container exists with appropriate partition key
        await database.Database.CreateContainerIfNotExistsAsync(
            id: ThreadContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: 400 // Minimum throughput for now
        );

        await database.Database.CreateContainerIfNotExistsAsync(
            id: LeaseContainerName,
            partitionKeyPath: "/id", // change feed leases must be partitioned by ID
            throughput: 400 // Minimum throughput for now'
        );

        await database.Database.CreateContainerIfNotExistsAsync(
            id: InstanceManagementContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: 400 // Minimum throughput for now'
        );

        await database.Database.CreateContainerIfNotExistsAsync(
            id: InstanceAssignmentsContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: 400 // Minimum throughput for now'
        );

        await database.Database.CreateContainerIfNotExistsAsync(
            id: AgentContextContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: 400 // Minimum throughput for now'
        );
    }
}

