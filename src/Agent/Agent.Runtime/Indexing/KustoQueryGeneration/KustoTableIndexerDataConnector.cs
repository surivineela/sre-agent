// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

namespace Agent.Runtime.Indexing.KustoQueryGeneration
{
    using System.Threading;
    using Agent.Core.Clients.Storage;
    using Agent.Core.Configuration;
    using Agent.Core.Interfaces;
    using Agent.Core.Models.Search;
    using Agent.Runtime.DataConnectors;
    using Azure.Core;
    using Microsoft.DurableTask;
    using Microsoft.DurableTask.Client;
    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    [DataConnector("KustoDataIndexer")]
    public class KustoTableIndexerDataConnector : IDataConnector
    {
        private readonly DurableTaskClient _durableTaskClient;
        private readonly ILogger<KustoTableIndexerDataConnector> _logger;
        private readonly IChatClient _chatClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly KustoMetadataIndex _kustoMetadataIndex;
        private readonly IAzureBlobStorageClient _azureBlobStorageClient;

        private DataConnectorSettings? _dataConnectorSettings;

        /// <summary>
        /// Each activity that the orchestrator calls needs this.
        /// However, we can't use dependency injection because this holds a KustoClient that needs to know auth information at creation time.
        /// We don't know auth info until InitAsync is called, so we created it there and make it available to all the activities through a static property.
        /// This is temporary until we move away from DTS and we can use a class member instance instead.
        /// </summary>
        internal static KustoTableSummarizer? KustoSummarizer;

        public KustoTableIndexerDataConnector(
            IChatClient chatClient,
            ILoggerFactory loggerFactory,
            KustoMetadataIndex kustmetadataindex,
            IAuthenticationService authService,
            IOptions<StorageSettings> storageSettings,
            DurableTaskClient durableTaskClient)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<KustoTableIndexerDataConnector>();
            _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
            _durableTaskClient = durableTaskClient ?? throw new ArgumentNullException(nameof(durableTaskClient));
            _kustoMetadataIndex = kustmetadataindex ?? throw new ArgumentNullException(nameof(kustmetadataindex));

            TokenCredential credential = authService.GetStorageCredential();
            _azureBlobStorageClient = new AzureBlobStorageClient(new Uri(storageSettings.Value.BlobEndpoint), credential);
        }

        public TimeSpan Interval
        {
            get
            {
                return TimeSpan.FromHours(12);
            }
        }

        public Task InitAsync(DataConnectorSettings settings, CancellationToken stoppingToken)
        {
            _dataConnectorSettings = settings ?? throw new ArgumentNullException(nameof(settings));

            _logger.LogInternalInformation($"Using managed identity resource ID {settings.Identity} for Kusto summarizer.");

            KustoSummarizer = new KustoTableSummarizer(_chatClient, new Uri(settings.DataSource), settings.Identity, _loggerFactory);

            return Task.CompletedTask;
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            Uri clusterUri = new Uri(_dataConnectorSettings!.DataSource);

            _logger.LogInternalInformation("Processing cluster: {ClusterUri}", clusterUri);

            try
            {
                await _azureBlobStorageClient.CreateContainerIfNotExistAsync("kustometadata", Azure.Storage.Blobs.Models.PublicAccessType.None);
                await _azureBlobStorageClient.CreateContainerIfNotExistAsync("kustometadataexamplequeries", Azure.Storage.Blobs.Models.PublicAccessType.None);

                await _kustoMetadataIndex.CreateOrUpdateIndex<KustoTableMetadata>(
                    indexName: "kustometadata-index",
                    blobContainer: "kustometadata");

                await _kustoMetadataIndex.CreateOrUpdateIndex<KustoExampleQueryDocument>(
                    indexName: "kustoexamplequery-index",
                    blobContainer: "kustometadataexamplequeries");

                // Create a unique orchestration instance ID for this table
                string orchestrationInstanceId = $"KustoTableIndexer-{clusterUri.Host}";

                // Check if there is an orchestration running for this table
                OrchestrationMetadata? existingInstance = await _durableTaskClient.GetInstanceAsync(orchestrationInstanceId, stoppingToken);

                KustoConnectionInfo kustoTableIndexInput = new KustoConnectionInfo
                (
                    ClusterUri: clusterUri,
                    ManagedIdentityClientId: string.Empty
                );

                StartOrchestrationOptions startOptions = new StartOrchestrationOptions(orchestrationInstanceId);

                if (existingInstance == null)
                {
                    _logger.LogInternalInformation("Starting new orchestration for cluster: {ClusterUri}", clusterUri);

                    await _durableTaskClient.ScheduleNewKustoTableIndexerOrchestratorInstanceAsync(kustoTableIndexInput, startOptions);
                }
                else if (existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
                {
                    // Check if the last update time is older than 12 hours
                    if (existingInstance.LastUpdatedAt < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(12)))
                    {
                        _logger.LogInternalInformation("Restarting completed orchestration for table: {ClusterUri}", clusterUri);

                        await _durableTaskClient.ScheduleNewKustoTableIndexerOrchestratorInstanceAsync(kustoTableIndexInput, startOptions);
                    }
                    else
                    {
                        _logger.LogInternalInformation("Orchestration for table {ClusterUri} completed recently, skipping", clusterUri);
                    }
                }
                else if (existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                         existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    // Restart failed orchestrations
                    _logger.LogInternalWarning("Restarting failed orchestration for table: {ClusterUri}", clusterUri);

                    await _durableTaskClient.ScheduleNewKustoTableIndexerOrchestratorInstanceAsync(kustoTableIndexInput, startOptions);
                }
                else
                {
                    // Orchestration is running, do nothing
                    _logger.LogInternalInformation("Orchestration for table {ClusterUri} is already running", clusterUri);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error processing table: {ClusterUri}", clusterUri);
            }
        }
    }
}
