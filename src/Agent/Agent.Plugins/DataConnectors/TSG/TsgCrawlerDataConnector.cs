// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
using Agent.Core.Interfaces;
using Agent.Framework.Reasoning.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.DataConnectors.TSG
{
    /// <summary>
    /// Data connector for crawling TSG (Troubleshooting Guide) documentation and indexing it for search
    /// </summary>
    [DataConnector("TsgCrawler")]
    public class TsgCrawlerDataConnector : IDataConnector
    {
        private const string DocumentsRootPath = "documents";
        private const int DefaultBatchSize = 10;

        private readonly ILogger<TsgCrawlerDataConnector> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly DataConnectorIndex _tsgMetadataIndex;
        private readonly DataConnectorStorage<TsgCrawlerDataConnector> _storage;
        private readonly IAuthenticationService _authService;

        private DataConnectorInstanceSettings? _dataConnectorInstanceSettings;
        private TsgAzureDevOpsClient? _azureDevOpsClient;
        private readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);

        public TsgCrawlerDataConnector(
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            IHostEnvironment hostEnvironment,
            DataConnectorIndex tsgMetadataIndex,
            DataConnectorStorage<TsgCrawlerDataConnector> storage,
            IAuthenticationService authService)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<TsgCrawlerDataConnector>();
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            _tsgMetadataIndex = tsgMetadataIndex;
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _authService = authService;
        }

        /// <summary>
        /// How often this data connector should run
        /// </summary>
        public TimeSpan Interval => TimeSpan.FromHours(24); // Run once per day

        /// <summary>
        /// Initialize the TSG data connector
        /// </summary>
        public Task InitAsync(DataConnectorInstanceSettings instanceSettings, CancellationToken stoppingToken)
        {
            _dataConnectorInstanceSettings = instanceSettings ?? throw new ArgumentNullException(nameof(instanceSettings));

            _logger.LogInternalInformation($"Initializing TSG crawler with authentication type and identity: {instanceSettings.Identity}");

            // Initialize Azure DevOps client with ConnectorAuthSettings
            var repositoryInfo = TsgDocumentHelper.ParseAzureDevOpsUri(new Uri(instanceSettings.DataSource));
            if (repositoryInfo == null)
            {
                throw new ArgumentException($"Invalid Azure DevOps URI: {instanceSettings.DataSource}", nameof(instanceSettings));
            }

            var azureDevOpsSettings = new AzureDevOpsSettings
            {
                Organization = repositoryInfo.Value.Organization,
                ProjectName = repositoryInfo.Value.ProjectName,
                RepositoryName = repositoryInfo.Value.RepositoryName
            };

            var authSettings = CreateAuthSettings(instanceSettings.Identity, _hostEnvironment.IsDevelopment());

            _azureDevOpsClient = new TsgAzureDevOpsClient(
                _logger,
                _httpClientFactory,
                azureDevOpsSettings,
                authSettings,
                _authService);

            return Task.CompletedTask;
        }

        private static ConnectorAuthSettings CreateAuthSettings(string? managedIdentityResourceId, bool isDevelopment)
        {
            return string.IsNullOrEmpty(managedIdentityResourceId) ?
                new ConnectorAuthSettings()
                {
                    AuthenticationType = isDevelopment ? ConnectorAuthType.User : ConnectorAuthType.ManagedIdentity
                } :
                new ConnectorAuthSettings()
                {
                    AuthenticationType = ConnectorAuthType.UAMI,
                    ManagedIdentityResourceId = managedIdentityResourceId
                };
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            // Ensure only one processing operation runs at a time
            if (!await _processingLock.WaitAsync(100, stoppingToken))
            {
                _logger.LogInternalInformation("TSG crawler is already running, skipping this execution.");
                return;
            }

            try
            {
                Uri dataSourceUri = new Uri(_dataConnectorInstanceSettings!.DataSource);
                _logger.LogInternalInformation("Processing TSG repository: {DataSource}", dataSourceUri);

                await ProcessRepositoryAsync(dataSourceUri, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to process TSG repository");
                throw;
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private async Task ProcessRepositoryAsync(Uri dataSourceUri, CancellationToken stoppingToken)
        {
            // Parse the data source URI to extract Azure DevOps information
            var repositoryInfo = TsgDocumentHelper.ParseAzureDevOpsUri(dataSourceUri);
            if (repositoryInfo == null)
            {
                _logger.LogInternalError("Failed to parse Azure DevOps URI: {DataSource}", dataSourceUri);
                return;
            }

            // Discover all files in the repository
            _logger.LogInternalInformation("Discovering files in repository {Organization}/{Project}/{Repository}",
                repositoryInfo.Value.Organization, repositoryInfo.Value.ProjectName, repositoryInfo.Value.RepositoryName);

            var filePaths = await _azureDevOpsClient!.GetAllFilesAsync("/");
            var textFiles = filePaths.Where(TsgDocumentHelper.IsPlainTextFile).ToList();

            _logger.LogInternalInformation("Discovered {FileCount} text files in repository {Repository}",
                textFiles.Count, repositoryInfo.Value.RepositoryName);

            // Process files in parallel batches
            var totalProcessedDocuments = 0;
            var batchSize = DefaultBatchSize;

            var fileBatches = textFiles
                .Select((filePath, index) => new { FilePath = filePath, Index = index })
                .GroupBy(x => x.Index / batchSize)
                .Select(g => g.Select(x => x.FilePath).ToList());

            foreach (var batch in fileBatches)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                var batchResults = await ProcessFileBatchAsync(batch, repositoryInfo.Value, dataSourceUri, stoppingToken);
                var validDocuments = batchResults.Where(doc => doc != null).Cast<TsgDocumentMetadata>().ToList();

                // Upload processed documents from this batch immediately
                if (validDocuments.Count > 0)
                {
                    await UploadDocumentBatchAsync(validDocuments);
                    totalProcessedDocuments += validDocuments.Count;
                }

                _logger.LogInternalInformation("Processed and uploaded batch with {ValidCount} valid documents out of {TotalCount} files",
                    validDocuments.Count, batch.Count);
            }

            // Start the indexer after processing all documents
            _logger.LogInternalInformation("Running indexer for {TotalDocuments} processed documents", totalProcessedDocuments);
            await RunIndexerAsync();

            _logger.LogInternalInformation("TSG crawler completed successfully. Processed {TotalDocuments} documents from {Repository}",
                totalProcessedDocuments, repositoryInfo.Value.RepositoryName);
        }

        private async Task<List<TsgDocumentMetadata?>> ProcessFileBatchAsync(
            List<string> fileBatch,
            (string Organization, string ProjectName, string RepositoryName) repositoryInfo,
            Uri dataSourceUri,
            CancellationToken stoppingToken)
        {
            var batchTasks = fileBatch.Select(async filePath =>
            {
                try
                {
                    if (stoppingToken.IsCancellationRequested)
                        return null;

                    _logger.LogInternalInformation("debug log tsgcrawler filepath:{filepath}", filePath);

                    return await ProcessSingleFileAsync(filePath, repositoryInfo, dataSourceUri);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to process file {FilePath}", filePath);
                    return null;
                }
            });

            return (await Task.WhenAll(batchTasks)).ToList();
        }

        private async Task<TsgDocumentMetadata?> ProcessSingleFileAsync(
            string filePath,
            (string Organization, string ProjectName, string RepositoryName) repositoryInfo,
            Uri dataSourceUri)
        {
            try
            {
                var fileContent = await _azureDevOpsClient!.ReadFileAsync(filePath);

                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    return null;
                }

                var azureDevOpsSettings = new AzureDevOpsSettings
                {
                    Organization = repositoryInfo.Organization,
                    ProjectName = repositoryInfo.ProjectName,
                    RepositoryName = repositoryInfo.RepositoryName
                };

                return TsgDocumentHelper.ParseTsgDocument(filePath, fileContent, azureDevOpsSettings, dataSourceUri);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to process file {FilePath}", filePath);
                return null;
            }
        }

        private async Task UploadDocumentBatchAsync(List<TsgDocumentMetadata> documents)
        {
            _logger.LogInternalInformation("Uploading {DocumentCount} TSG documents to blob storage", documents.Count);

            var uploadTasks = documents.Select(async doc =>
            {
                try
                {
                    await UploadTsgDocumentToBlob(doc);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to upload document {DocumentId}", doc.Id);
                    return false;
                }
            });

            var results = await Task.WhenAll(uploadTasks);
            var successCount = results.Count(r => r);
            var failureCount = results.Count(r => !r);

            _logger.LogInternalInformation("Document upload completed: {SuccessCount} succeeded, {FailureCount} failed",
                successCount, failureCount);
        }

        internal async Task RunIndexerAsync()
        {
            await _tsgMetadataIndex.RunIndexerAsync();
        }

        /// <summary>
        /// Search for TSG documents
        /// </summary>
        public async Task<List<TsgDocumentMetadata>> SearchAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
        {
            try
            {
                var results = new List<TsgDocumentMetadata>();

                await foreach (var searchResult in _tsgMetadataIndex.SearchAsync<TsgDocumentMetadata>(query, string.Empty, maxResults))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    results.Add(searchResult.OriginalDocument);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to search TSG documents for query: {Query}", query);
                throw;
            }
        }

        internal Task UploadTsgDocumentToBlob(TsgDocumentMetadata data)
        {
            return _storage.UploadBlobContentsAsync(GetBlobNameForTsgDocument(data), data);
        }

        private static string GetBlobNameForTsgDocument(TsgDocumentMetadata metadata)
        {
            string repoName = !string.IsNullOrWhiteSpace(metadata.Source)
                ? TsgDocumentHelper.GetSafeRepositoryName(new Uri(metadata.Source))
                : "unknown-sourcerepo";

            return $"{DocumentsRootPath}/{repoName}/{metadata.Id}.json";
        }
    }

    /// <summary>
    /// Settings for Azure DevOps connection
    /// </summary>
    public class AzureDevOpsSettings
    {
        public required string Organization { get; init; }
        public required string ProjectName { get; init; }
        public required string RepositoryName { get; init; }
    }
}
