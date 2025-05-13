// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Data.DataModels;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories
{
    public class CosmosDbAppHealthHistoryRepository : IAppHealthHistoryRepository
    {
        private readonly Container _container;
        private readonly ILogger<CosmosDbAppHealthHistoryRepository> _logger;
        private readonly string _databaseName;
        private readonly CosmosClient _client;

        public CosmosDbAppHealthHistoryRepository(
            CosmosClient cosmosClient,
            string databaseName,
            ILogger<CosmosDbAppHealthHistoryRepository> logger)
        {
            _logger = logger;
            _databaseName = databaseName;
            _client = cosmosClient;
            _container = _client.GetContainer(_databaseName, AppHealthHistoryDocument.ContainerName);
        }

        public async Task<AppHealthHistoryDocument> UpdateAppHealthHistoryAsync(string appId, string appName, string resourceType, AppHealthInfo healthInfo)
        {
            try
            {
                // First check if a document already exists for this app group
                var existingDocument = await GetAppHealthHistoryAsync(appId);
                
                // Create health info data point
                var healthInfoData = new AppHealthHistoryDocument.AppHealthInfoData
                {
                    LastDataCaptureTimeStampInUTC = healthInfo.LastDataCaptureTimeStampInUTC,
                    Health = healthInfo.Health,
                    Availability = healthInfo.Availability,
                    AvgCpuUsage = healthInfo.AvgCpuUsage,
                    AvgMemoryUsage = healthInfo.AvgMemoryUsage,
                    Transactions = healthInfo.Transactions
                };
                
                if (existingDocument == null)
                {
                    // Create a new document
                    var document = new AppHealthHistoryDocument(
                        Guid.NewGuid().ToString(),
                        appId,
                        appName,
                        resourceType)
                    {
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    // Add the data point to history
                    document.HistoryData.Add(healthInfoData);
                    
                    var response = await _container.CreateItemAsync(document, new PartitionKey(document.PartitionKey));
                    return response.Resource;
                }
                else
                {
                    // Update existing document
                    existingDocument.LastUpdated = DateTime.UtcNow;
                    existingDocument.AppName = appName; // Update name in case it changed
                    existingDocument.ResourceType = resourceType; // Update type in case it changed
                    
                    // Add the new data point to history
                    existingDocument.HistoryData.Add(healthInfoData);
                    
                    var response = await _container.ReplaceItemAsync(
                        existingDocument,
                        existingDocument.Id,
                        new PartitionKey(existingDocument.PartitionKey));
                    
                    return response.Resource;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error updating app health history for app {AppId}", appId);
                throw;
            }
        }

        public async Task<AppHealthHistoryDocument> GetAppHealthHistoryAsync(string appId)
        {
            try
            {
                var query = _container.GetItemLinqQueryable<AppHealthHistoryDocument>()
                    .Where(doc => doc.DocumentType == "AppHealthHistory" && doc.AppId == appId);
                
                var iterator = query.ToFeedIterator();
                
                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    return response.FirstOrDefault();
                }
                
                return null;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting app health history for app {AppId}", appId);
                throw;
            }
        }

        public async Task<(int DocumentsUpdated, int DataPointsRemoved)> PruneAppHealthHistoryAsync(DateTime olderThan)
        {
            try
            {
                _logger.LogInternalInformation("Pruning app health history data points older than {OlderThan}", olderThan);
                
                var query = _container.GetItemLinqQueryable<AppHealthHistoryDocument>()
                    .Where(doc => doc.DocumentType == "AppHealthHistory");
                
                var iterator = query.ToFeedIterator();
                
                int documentsUpdated = 0;
                int totalPointsRemoved = 0;
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var document in response)
                    {
                        try
                        {
                            // Count data points to remove
                            int initialCount = document.HistoryData.Count;
                            
                            // Remove old data points
                            document.HistoryData = document.HistoryData
                                .Where(dp => dp.LastDataCaptureTimeStampInUTC >= olderThan)
                                .ToList();
                            
                            int pointsRemoved = initialCount - document.HistoryData.Count;
                            
                            // Only update if points were removed
                            if (pointsRemoved > 0)
                            {
                                await _container.ReplaceItemAsync(
                                    document,
                                    document.Id,
                                    new PartitionKey(document.PartitionKey));
                                
                                documentsUpdated++;
                                totalPointsRemoved += pointsRemoved;
                            }
                        }
                        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            // Document was deleted, continue
                        }
                    }
                }

                _logger.LogInternalInformation("Pruned {TotalPointsRemoved} app health history data points from {DocumentsUpdated} documents", 
                    totalPointsRemoved, documentsUpdated);
                
                return (documentsUpdated, totalPointsRemoved);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error pruning old app health history data points");
                throw;
            }
        }
    }
} 
