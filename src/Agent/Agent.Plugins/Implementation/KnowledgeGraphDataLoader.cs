// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Extension methods for loading knowledge graph seed data into the storage provider.
/// </summary>
public static class KnowledgeGraphDataLoader
{
    /// <summary>
    /// Loads knowledge graph data from a JSON file into the Cosmos DB container.
    /// This method reads the JSON file and uses CreateEntitiesAsync and CreateRelationsAsync
    /// to populate the knowledge graph.
    /// </summary>
    /// <param name="serviceProvider">The service provider to get required services</param>
    /// <param name="configuration">The configuration to read seed data path</param>
    public static async Task LoadKnowledgeGraphSeedDataAsync(this IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var kgJsonPath = configuration["KnowledgeGraph:SeedDataPath"];
        if (string.IsNullOrEmpty(kgJsonPath) || !File.Exists(kgJsonPath))
        {
            // No seed data configured or file doesn't exist, skip loading
            return;
        }

        var storageProvider = serviceProvider.GetService<IKnowledgeGraphStorageProvider>();
        if (storageProvider == null)
        {
            // Storage provider not registered yet, skip loading
            return;
        }

        var logger = serviceProvider.GetService<ILogger<CosmosDbKnowledgeGraphStorageProvider>>();

        try
        {
            logger?.LogInformation("Loading knowledge graph seed data from {Path}", kgJsonPath);

            var jsonContent = await File.ReadAllTextAsync(kgJsonPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var knowledgeGraph = JsonSerializer.Deserialize<KnowledgeGraph>(jsonContent, options);
            if (knowledgeGraph == null)
            {
                logger?.LogWarning("Failed to deserialize knowledge graph from {Path}", kgJsonPath);
                return;
            }

            // Create entities first
            if (knowledgeGraph.Entities?.Count > 0)
            {
                logger?.LogInformation("Creating {Count} entities from seed data", knowledgeGraph.Entities.Count);
                var createdEntities = await storageProvider.CreateEntitiesAsync(knowledgeGraph.Entities);
                logger?.LogInformation("Created {Count} new entities (duplicates skipped)", createdEntities.Count);
            }

            // Then create relations
            if (knowledgeGraph.Relations?.Count > 0)
            {
                logger?.LogInformation("Creating {Count} relations from seed data", knowledgeGraph.Relations.Count);
                var createdRelations = await storageProvider.CreateRelationsAsync(knowledgeGraph.Relations);
                logger?.LogInformation("Created {Count} new relations (duplicates skipped)", createdRelations.Count);
            }

            logger?.LogInformation("Knowledge graph seed data loading completed");
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "JSON parsing error while loading knowledge graph seed data from {Path}", kgJsonPath);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error loading knowledge graph seed data from {Path}", kgJsonPath);
        }
    }
}
