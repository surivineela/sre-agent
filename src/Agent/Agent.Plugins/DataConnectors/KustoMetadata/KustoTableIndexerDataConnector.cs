// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

namespace Agent.Plugins.DataConnectors.KustoMetadata
{
    using System.Collections.Concurrent;
    using System.Threading;
    using Agent.Core.Configuration;
    using Agent.Core.DataConnectors;
    using Microsoft.DurableTask;
    using Microsoft.DurableTask.Client;
    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.Logging;

    [DataConnector("KustoDataIndexer")]
    public class KustoTableIndexerDataConnector : IDataConnector
    {
        /// <summary>
        /// Each activity that the orchestrator calls needs this.
        /// However, we can't use dependency injection because this holds a KustoClient that needs to know auth information at creation time.
        /// We don't know auth info until InitAsync is called, so we created it there and make it available to all the activities through a static property.
        /// This is temporary until we move away from DTS and we can use a class member instance instead.
        /// </summary>
        private static ConcurrentDictionary<string, KustoTableIndexerDataConnector> Instances { get; } = new ConcurrentDictionary<string, KustoTableIndexerDataConnector>();

        private const string TableRootPath = "tables";
        private const string ExampleQueriesRootPath = "examples";

        private readonly DurableTaskClient _durableTaskClient;
        private readonly ILogger<KustoTableIndexerDataConnector> _logger;
        private readonly IChatClient _chatClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DataConnectorIndex _kustoMetadataIndex;
        private readonly DataConnectorStorage<KustoTableIndexerDataConnector> _storage;

        private DataConnectorInstanceSettings? _dataConnectorInstanceSettings;

        internal static KustoTableIndexerDataConnector GetDataConnector(string name)
        {
            if (Instances.TryGetValue(name, out KustoTableIndexerDataConnector? instance))
            {
                return instance;
            }

            throw new KeyNotFoundException($"No KustoTableIndexerDataConnector found with name '{name}'.");
        }

        internal KustoTableSummarizer? KustoSummarizer { get; private set; }

        public KustoTableIndexerDataConnector(
            IChatClient chatClient,
            ILoggerFactory loggerFactory,
            DataConnectorIndex kustoMetadataIndex,
            DataConnectorStorage<KustoTableIndexerDataConnector> storage,
            DurableTaskClient durableTaskClient)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<KustoTableIndexerDataConnector>();
            _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
            _durableTaskClient = durableTaskClient ?? throw new ArgumentNullException(nameof(durableTaskClient));
            _kustoMetadataIndex = kustoMetadataIndex;
            _storage = storage;
        }

        public TimeSpan Interval
        {
            get
            {
                return TimeSpan.FromHours(12);
            }
        }

        public Task InitAsync(DataConnectorInstanceSettings instanceSettings, CancellationToken stoppingToken)
        {
            _dataConnectorInstanceSettings = instanceSettings ?? throw new ArgumentNullException(nameof(instanceSettings));

            _logger.LogInternalInformation($"Using managed identity resource ID {instanceSettings.Identity} for Kusto summarizer.");

            KustoSummarizer = new KustoTableSummarizer(_chatClient, new Uri(instanceSettings.DataSource), instanceSettings.Identity, _loggerFactory);

            // temporary workaround for DTS task classes 
            Instances[instanceSettings.Name] = this;

            return Task.CompletedTask;
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            Uri clusterUri = new Uri(_dataConnectorInstanceSettings!.DataSource);

            _logger.LogInternalInformation("Processing cluster: {ClusterUri}", clusterUri);

            // Create a unique orchestration instance ID for this table
            string orchestrationInstanceId = $"KustoTableIndexer-{clusterUri.Host}-{_dataConnectorInstanceSettings.Name}";

            // Check if there is an orchestration running for this table
            OrchestrationMetadata? existingInstance = await _durableTaskClient.GetInstanceAsync(orchestrationInstanceId, stoppingToken);

            KustoConnectionInfo kustoTableIndexInput = new KustoConnectionInfo
            (
                DataConnectorName: _dataConnectorInstanceSettings!.Name,
                ClusterUri: clusterUri,
                ManagedIdentityClientId: string.Empty,
                DatabaseFilter: [],
                TableFilter: []
            );

            StartOrchestrationOptions startOptions = new StartOrchestrationOptions(orchestrationInstanceId);

            if (existingInstance == null)
            {
                _logger.LogInternalInformation("Starting new orchestration for Kusto cluster: {ClusterUri}", clusterUri);

                await _durableTaskClient.ScheduleNewKustoTableIndexerOrchestratorInstanceAsync(kustoTableIndexInput, startOptions);
            }
            else if (existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
            {
                // Check if the last update time is older than 12 hours
                if (existingInstance.LastUpdatedAt < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(24)))
                {
                    _logger.LogInternalInformation("Restarting completed orchestration for Kusto cluster: {ClusterUri}", clusterUri);

                    await _durableTaskClient.ScheduleNewKustoTableIndexerOrchestratorInstanceAsync(kustoTableIndexInput, startOptions);
                }
                else
                {
                    _logger.LogInternalInformation("Orchestration for Kusto cluster {ClusterUri} completed recently, skipping", clusterUri);
                }
            }
            else if (existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                        existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
            {
                // Restart failed orchestrations
                _logger.LogInternalWarning("Restarting failed orchestration for Kusto cluster: {ClusterUri}", clusterUri);

                await _durableTaskClient.ScheduleNewKustoTableIndexerOrchestratorInstanceAsync(kustoTableIndexInput, startOptions);
            }
            else
            {
                // Orchestration is running, do nothing
                _logger.LogInternalInformation("Orchestration for Kusto cluster {ClusterUri} is already running", clusterUri);
            }
        }

        internal async Task RunIndexerAsync()
        {
            await _kustoMetadataIndex.RunIndexerAsync();
        }

        internal Task UploadKustoMetadataToBlob(KustoTableMetadata data)
        {
            return UploadJsonToBlob(GetBlobNameForKustoTableMetadata(data), BinaryData.FromObjectAsJson(data));
        }

        internal Task UploadKustoExampleQueriesToBlob(KustoExampleQueryDocument data)
        {
            return UploadJsonToBlob(GetBlobNameForKustoExampleQuery(data), BinaryData.FromObjectAsJson(data));
        }

        internal static string GetSafeKustoClusterName(Uri clusterUri)
        {
            return clusterUri.Host
                .ToLowerInvariant()
                .Replace(".kusto.windows.net", string.Empty)
                .Replace(".", "-");
        }

        private async Task UploadJsonToBlob(string blobName, BinaryData data)
        {
            await _storage.UploadBlobContentsAsync(blobName, data);
        }

        private static string GetBlobNameForKustoTableMetadata(KustoTableMetadata metadata)
        {
            return $"{TableRootPath}/{GetSafeKustoClusterName(new Uri(metadata.ClusterUri))}_{metadata.DatabaseName}/{metadata.TableName}.json";
        }

        private static string GetBlobNameForKustoExampleQuery(KustoExampleQueryDocument exampleQuery)
        {
            return $"{ExampleQueriesRootPath}/{GetSafeKustoClusterName(new Uri(exampleQuery.ClusterUri))}_{exampleQuery.DatabaseName}/{exampleQuery.TableName}_example_queries.json";
        }
    }
}
