using Agent.Core.Interfaces;
using Agent.Core.Configuration;
using Agent.Data.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Identity;

namespace Agent.Data;

public static class AgentDataConfiguration
{
    public const string ContainerName = "documents";

    public static IServiceCollection AddCosmosClient(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var federationSettings = serviceProvider.GetRequiredService<FederationSettings>();

            var cosmosAccountName = cosmosDbSettings.Docs.AccountName;
            var cosmosAccountApiKey = cosmosDbSettings.Docs.ApiKey;
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

            if (string.IsNullOrEmpty(federationSettings.ClientId))
            {
                var cosmosConnectionString = $"AccountEndpoint={endpoint};AccountKey={cosmosAccountApiKey};";
                return new CosmosClient(cosmosConnectionString, cosmosOptions);
            }
            else
            {
                var credOptions = new WorkloadIdentityCredentialOptions()
                {
                    ClientId = federationSettings.ClientId,
                    TenantId = federationSettings.TenantId,
                    AuthorityHost = new Uri(federationSettings.AuthorityHost),
                };

                var credential = new WorkloadIdentityCredential(credOptions);
                return new CosmosClient(endpoint, credential, cosmosOptions);
            }
        });

        // Register the repository
        serviceCollection.AddSingleton<IThreadRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<CosmosDbThreadRepository>>();
            return new CosmosDbThreadRepository(cosmosClient, cosmosDatabaseName, ContainerName, logger);
        });

        // Add Thread Orchestration Mapping repository registration
        serviceCollection.AddSingleton<IThreadOrchestrationMappingRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;
            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();

            return new CosmosDbThreadOrchestrationMappingRepository(
                cosmosClient,
                cosmosDatabaseName,
                ContainerName); // Use the same container as the thread repository
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
            id: ContainerName,
            partitionKeyPath: "/partitionKey",
            throughput: 400 // Minimum throughput for now
        );
    }
}
