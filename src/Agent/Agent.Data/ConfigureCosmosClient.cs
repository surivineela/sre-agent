using Agent.Core.Configuration;
using Agent.Data.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Data;

public static class AgentDataConfiguration
{
    public const string ContainerName = "documents";

    public static IServiceCollection AddCosmosClient(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();

            var cosmosAccountName = cosmosDbSettings.Docs.AccountName;
            var cosmosAccountApiKey = cosmosDbSettings.Docs.ApiKey;
            var domainSuffix = cosmosDbSettings.Docs.DomainSuffix;

            var cosmosConnectionString = $"AccountEndpoint=https://{cosmosAccountName}.{domainSuffix};AccountKey={cosmosAccountApiKey};";

            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;


            return new CosmosClient(cosmosConnectionString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        });

        // Register the repository
        serviceCollection.AddSingleton<IThreadRepository>(serviceProvider =>
        {
            var cosmosDbSettings = serviceProvider.GetRequiredService<CosmosDBSettings>();
            var cosmosDatabaseName = cosmosDbSettings.Docs.Database;

            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            return new CosmosDbThreadRepository(cosmosClient, cosmosDatabaseName, ContainerName);
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
