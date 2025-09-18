// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.AgentMemory;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Data;

public static class AgentMemoryConfiguration
{
    public const string AgentMemoryBlobClient = "agentMemoryBlobClient";
    public const string AgentMemoryAISearchClient = "agentMemoryAISearchClient";
    public const string AgentMemoryIndexClient = "agentMemoryIndexClient";
    public const string AgentMemoryIndexerClient = "agentMemoryIndexerClient";

    public static IServiceCollection AddAgentMemory(this IServiceCollection serviceCollection, bool enableAgentMemory)
    {
        if (enableAgentMemory)
        {
            // Only register blob client if storage account is enabled
            serviceCollection.AddKeyedSingleton(AgentMemoryBlobClient, (serviceProvider, _) =>
            {
                var agentMemorySettings = serviceProvider.GetRequiredService<AgentMemorySettings>();

                // If storage account is disabled, return a dummy blob client
                if (!agentMemorySettings.StorageAccountEnabled)
                {
                    return new BlobServiceClient(new Uri("https://dummy.blob.core.windows.net"));
                }

                var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
                var tokenCredential = authService.GetAgentMemoryBlobStorageCredential();
                return new BlobServiceClient(
                    new Uri($"https://{agentMemorySettings.StorageAccountName}.{agentMemorySettings.BlobStorageDomainSuffix}"),
                    tokenCredential);
            });

            // Register the repository
            serviceCollection.AddKeyedSingleton(AgentMemoryAISearchClient, (serviceProvider, _) =>
            {
                var agentMemorySettings = serviceProvider.GetRequiredService<AgentMemorySettings>();

                var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
                var tokenCredential = authService.GetAgentMemoryAzureAISearchCredential();

                var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();

                var searchClientOptions = new SearchClientOptions()
                {
                    Serializer = Constants.JsonSerializer
                };

                return new SearchClient(
                    new Uri($"https://{agentMemorySettings.AzureAISearchName}.{agentMemorySettings.AzureAISearchDomainSuffix}"),
                    agentMemorySettings.AzureAISearchIndexName,
                    tokenCredential,
                    searchClientOptions);
            });

            serviceCollection.AddKeyedSingleton(AgentMemoryIndexClient, (serviceProvider, _) =>
            {
                var agentMemorySettings = serviceProvider.GetRequiredService<AgentMemorySettings>();

                var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
                var tokenCredential = authService.GetAgentMemoryAzureAISearchCredential();

                return new SearchIndexClient(
                    new Uri($"https://{agentMemorySettings.AzureAISearchName}.{agentMemorySettings.AzureAISearchDomainSuffix}"),
                    tokenCredential);
            });

            serviceCollection.AddKeyedSingleton(AgentMemoryIndexerClient, (serviceProvider, _) =>
            {
                var agentMemorySettings = serviceProvider.GetRequiredService<AgentMemorySettings>();

                // If storage account is disabled, return a dummy blob client
                if (!agentMemorySettings.StorageAccountEnabled)
                {
                    return new SearchIndexerClient(
                        new Uri("https://dummy.search.windows.net"),
                        new AzureKeyCredential("dummy"));
                }

                var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
                var tokenCredential = authService.GetAgentMemoryAzureAISearchCredential();

                return new SearchIndexerClient(
                    new Uri($"https://{agentMemorySettings.AzureAISearchName}.{agentMemorySettings.AzureAISearchDomainSuffix}"),
                    tokenCredential);
            });

            serviceCollection.AddSingleton<IAgentMemoryClient, AgentMemoryClient>();
            serviceCollection.AddSingleton<ISearchIndexService, SearchIndexService>();
        }
        else
        {
            // If agent memory is not enabled, register a dummy implementation
            serviceCollection.AddSingleton<IAgentMemoryClient, DummyAgentMemoryClient>();

            serviceCollection.AddSingleton<ISearchIndexService, DummySearchIndexService>();
        }


        return serviceCollection;
    }

    public static async Task SetupAgentMemoryIndexAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var indexClient = scope.ServiceProvider.GetRequiredService<IAgentMemoryClient>();
        await indexClient.SetupIndexerAsync();
    }
}

