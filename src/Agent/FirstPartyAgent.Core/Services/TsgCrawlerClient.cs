using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using FirstPartyAgent.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Agent.Core.Configuration;

namespace FirstPartyAgent.Core.Services
{
    public interface ITsgCrawlerClient
    {
        Task CrawlAndStoreRepositoryAsync();
    }

    public class NullableTsgCrawlerClient : ITsgCrawlerClient
    {
        public Task CrawlAndStoreRepositoryAsync() => Task.FromResult("TSG Crawler Client is Disabled.");
    }

    public class TsgCrawlerClient : ITsgCrawlerClient
    {
        private readonly IAzureDevOpsClient? _azureDevOpsClient;
        private readonly ILogger<TsgCrawlerClient> _logger;
        private readonly TsgCrawlerSettings _tsgCrawlerSettings;
        private readonly AzureSearchSettings _azureSearchSettings;
        private readonly IStorageService? _storageService;

        public TsgCrawlerClient(
            IHostEnvironment hostEnvironment,
            TsgCrawlerSettings tsgCrawlerSettings,
            ILogger<TsgCrawlerClient> logger)
        {
            _tsgCrawlerSettings = tsgCrawlerSettings;
            _logger = logger;
            _azureSearchSettings = _tsgCrawlerSettings.AiSearchSettings;

            if (tsgCrawlerSettings.Enabled)
            {
                _storageService = new StorageService(_tsgCrawlerSettings.TsgStorageSettings);
                _azureDevOpsClient = new AzureDevOpsRestClient(hostEnvironment, _tsgCrawlerSettings.DevOpsRepoSettings);
            }
        }

        public async Task CrawlAndStoreRepositoryAsync()
        {
            if (_tsgCrawlerSettings == null || !_tsgCrawlerSettings.Enabled || _storageService == null || _azureDevOpsClient == null)
            {
                _logger.LogWarning("TSG Crawler Client is disabled. Skipping repository crawl.");
                return;
            }

            _logger.LogInformation("Starting repository crawl and store process...");
            
            var allFiles = await GetAllFilesAsync(_tsgCrawlerSettings.TsgRootPath);
            _logger.LogInformation($"Found {allFiles.Count} files to process");
            
            // Use a semaphore to limit concurrent operations
            int maxConcurrentOperations = 10; // Adjust based on system capacity
            using var semaphore = new System.Threading.SemaphoreSlim(maxConcurrentOperations);
            
            var tasks = new List<Task>();
            int processedCount = 0;
            int failedCount = 0;
            
            foreach (var filePath in allFiles)
            {
                if (!IsPlainTextFile(filePath))
                {
                    _logger.LogWarning($"Skipping non-text file: {filePath}");
                    System.Threading.Interlocked.Increment(ref processedCount); // Increment processCount when plain text check fails
                    continue; // Skip non-text files
                }
                // Wait until we can enter the semaphore
                await semaphore.WaitAsync();
                
                // Create a task for processing this file
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // For text files, use the original method
                        var fileContent = await _azureDevOpsClient.ReadFileAsync(filePath);
                        await _storageService.WriteContentToStorage(_tsgCrawlerSettings.TsgStorageSettings.IcmAlertConfigsContainerName, filePath, fileContent);
                        
                        System.Threading.Interlocked.Increment(ref processedCount);
                        if (processedCount % 50 == 0)
                        {
                            _logger.LogInformation($"Progress: {processedCount}/{allFiles.Count} files processed");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Threading.Interlocked.Increment(ref failedCount);
                        _logger.LogError(ex, $"Failed to process file '{filePath}'");
                        // Don't rethrow, so other files can still be processed
                    }
                    finally
                    {
                        // Always release the semaphore when done
                        semaphore.Release();
                    }
                }));
            }
            
            // Wait for all tasks to complete with a generous timeout
            // This ensures we don't wait forever, but still give tasks time to complete
            var timeoutTask = Task.Delay(TimeSpan.FromHours(4)); // Adjust timeout as needed
            var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Operation timed out after 4 hours, some files may not have been processed");
            }
            
            _logger.LogInformation($"Repository crawl completed. Processed {processedCount} files successfully. Failed to process {failedCount} files.");
            
            // Only trigger the indexer if we processed at least some files
            if (processedCount > 0)
            {
                await TriggerSearchIndexerAsync();
            }
        }
        
        private async Task TriggerSearchIndexerAsync()
        {
            if (string.IsNullOrEmpty(_azureSearchSettings.SearchServiceUri) ||
                string.IsNullOrEmpty(_azureSearchSettings.SearchApiKeyOverride))
            {
                _logger.LogWarning("Azure AI Search settings (ServiceUri or ApiKey) are not configured. Skipping indexer run.");
                return;
            }

            var indexName = _tsgCrawlerSettings.IndexerName;
            _logger.LogInformation($"Attempting to run Azure AI Search indexer for index: '{indexName}'.");

            try
            {
                Uri serviceEndpoint = new Uri(_azureSearchSettings.SearchServiceUri);
                AzureKeyCredential credential = new AzureKeyCredential(_azureSearchSettings.SearchApiKeyOverride);
                SearchIndexerClient indexerClient = new SearchIndexerClient(serviceEndpoint, credential);

                // Run the indexer for the configured index
                await indexerClient.RunIndexerAsync(indexName);

                _logger.LogInformation($"Successfully triggered Azure AI Search indexer for index: '{indexName}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to run Azure AI Search indexer for index: '{indexName}'.");
                // Optionally rethrow or handle as appropriate for your application
            }
        }

        private async Task<List<string>> GetAllFilesAsync(string path)
        {
            if (_azureDevOpsClient == null)
            {
                _logger.LogWarning("Azure DevOps client is not initialized. Cannot retrieve files.");
                return new List<string>();
            }

            var allFiles = new List<string>();
            // Use Full recursion level to get all files in one request
            var result = await _azureDevOpsClient.ListFilesAsync(path, int.MaxValue, "Full");

            using (JsonDocument document = JsonDocument.Parse(result))
            {
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("value", out JsonElement valueArray) && valueArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in valueArray.EnumerateArray())
                    {
                        // Only add files, not folders
                        if (!(item.TryGetProperty("isFolder", out JsonElement isFolderElement) && isFolderElement.GetBoolean()))
                        {
                            if (item.TryGetProperty("path", out JsonElement pathElement))
                            {
                                var filePath = pathElement.GetString();
                                if (filePath != null)
                                {
                                    allFiles.Add(filePath);
                                }                                
                            }
                        }
                    }
                }
            }

            return allFiles;
        }

        /// <summary>
        /// Determines if a file is a plain text file (.txt, .json, or .md)
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if the file has a plain text extension, false otherwise</returns>
        private bool IsPlainTextFile(string filePath)
        {
            // Common plain text file extensions
            string[] plainTextExtensions = {
                ".txt", ".json", ".md"
            };
            
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return plainTextExtensions.Contains(extension);
        }
    }
}
