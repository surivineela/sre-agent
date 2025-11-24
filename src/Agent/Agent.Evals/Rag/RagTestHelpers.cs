// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Clients.Search;
using Agent.Core.Clients.Storage;
using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data;
using Agent.Data.AgentMemory;
using Agent.Framework;
using Agent.Plugins.Definitions;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services.Mcp;
using Agent.Runtime.ThreadEvaluator;
using Agent.Tests.Common.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Evals.Rag;

/// <summary>
/// Common helper methods for RAG evaluation tests.
/// </summary>
public static class RagTestHelpers
{
    /// <summary>
    /// Sets up the search index by deleting existing index, creating new one, and indexing trajectories from the specified data folder.
    /// Searches all subdirectories recursively for trajectory files matching pattern "traj_*.json".
    /// </summary>
    /// <param name="host">The host containing required services</param>
    /// <param name="dataFolderPath">Relative path from AppContext.BaseDirectory to the folder containing trajectory files</param>
    public static async Task SetupTestIndexAsync(
        TestContext testContext,
        IHost host,
        string dataFolderPath)
    {
        testContext.WriteLine("Setting up Data Connector index...");
        var dataConnectorIndex = host.Services.GetRequiredService<DataConnectorIndex>();
        var indexingClient = (SearchIndexingClient)host.Services.GetRequiredService<ISearchIndexingClient>();
        var dataConnectorIndexName = host.Services.GetRequiredService<IOptions<DataConnectorSettings>>().Value.Search.IndexName;

        await indexingClient.DeleteIndexIfExistsAsync(dataConnectorIndexName);
        await dataConnectorIndex.CreateOrUpdateIndex();

        testContext.WriteLine("Setting up Agent Memory Trajectory index...");
        // Rebuild the index
        var indexService = host.Services.GetRequiredService<ISearchIndexService>();
        await indexService.DeleteIndexIfExistsAsync();
        await host.Services.SetupAgentMemoryIndexAsync();

        // Resolve full data folder path
        var fullDataFolderPath = Path.Combine(AppContext.BaseDirectory, dataFolderPath);

        if (!Directory.Exists(fullDataFolderPath))
        {
            Assert.Fail($"Test data folder not found: {fullDataFolderPath}");
        }

        // Find trajectory files - search all subdirectories for traj_*.json files
        const string filePattern = "traj_*.json";
        var trajectoryFiles = Directory.GetFiles(fullDataFolderPath, filePattern, SearchOption.AllDirectories);

        if (trajectoryFiles.Length == 0)
        {
            Assert.Fail($"No trajectory files found in {fullDataFolderPath} matching pattern {filePattern}");
        }

        testContext.WriteLine($"Found {trajectoryFiles.Length} trajectory files to index.");

        // Index each trajectory
        foreach (var file in trajectoryFiles)
        {
            await IndexTrajectoryFileAsync(
                testContext,
                host,
                file);
        }

        testContext.WriteLine("Index setup completed.");
    }

    /// <summary>
    /// Indexes a single trajectory file into the search index.
    /// </summary>
    /// <param name="trajectoryFilePath">Full path to the trajectory file</param>
    /// <param name="embeddingGenerator">Embedding generator service</param>
    /// <param name="indexService">Search index service</param>
    /// <param name="logger">Logger for tracking indexing operations</param>
    /// <param name="useGuidForTrajectoryId">If true, uses a new GUID for trajectory ID; if false, uses filename without extension</param>
    private static async Task IndexTrajectoryFileAsync(
        TestContext testContext,
        IHost host,
        string trajectoryFilePath)
    {
        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(RagTestHelpers));

        var chatClientProvider = host.Services.GetRequiredService<IChatClientProvider>();
        var embeddingGenerator = chatClientProvider.EmbeddingModel;

        var indexService = host.Services.GetRequiredService<ISearchIndexService>();

        // Read and deserialize trajectory
        var content = await File.ReadAllTextAsync(trajectoryFilePath);
        var trajectory = WebJsonSerializer.DeserializeOrThrow<ProcessedTrajectoryOutput_v3>(content);

        // Generate embedding for the trajectory
        var embedding = await embeddingGenerator.GenerateVectorForAgentMemoryAsync(
            trajectory.SymptomsObserved,
            logger);

        // Create AgentMemory from trajectory
        var agentMemory = AgentMemory.FromTrajectory(
            trajectoryGuid: Guid.NewGuid(),
            trajectoryData: trajectory,
            embedding: [.. embedding.Span]);

        // Index the trajectory
        var indexed = await indexService.IndexContentAsync(agentMemory);
        Assert.IsTrue(indexed, "Failed to index trajectory");

        testContext.WriteLine($"Indexed: {agentMemory.Id} - {trajectory.Title}");
    }

    public static IHostApplicationBuilder AddAgentMemoryPlugin(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IAgentOutboundCommunicationService>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger<IAgentOutboundCommunicationService>();
            return new MockCommunicationService(logger);
        });
        builder.Services.AddSingleton<ISearchIndexingClient, SearchIndexingClient>();
        builder.Services.AddSingleton<IAzureBlobStorageClient, AzureBlobStorageClient>();
        builder.Services.AddSingleton<ISessionTransportFactory, SessionTransportFactory>();
        builder.Services.AddSingleton<IMcpConnectionEventManager, McpConnectionEventManager>();
        builder.Services.AddSingleton<IMcpAuthenticationService, McpAuthenticationService>();
        builder.RegisterDataConnectors();
        builder.Services.AddTransient<AgentMemoryPluginDefinition>();
        builder.Services.AddSingleton<RagEvaluator>();
        return builder;
    }
}
