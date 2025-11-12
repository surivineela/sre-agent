// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DataModels;

namespace Agent.Data.Repositories
{
    public interface IAppHealthHistoryRepository
    {
        /// <summary>
        /// Updates app health history by adding a new data point to the history array
        /// Creates a new document if one doesn't exist for the app group
        /// </summary>
        /// <param name="appId">ID of the app or resource</param>
        /// <param name="appName">Name of the app or resource</param>
        /// <param name="resourceType">Resource type</param>
        /// <param name="healthInfo">Health information data</param>
        /// <returns>The updated document</returns>
        Task<AppHealthHistoryDocument> UpdateAppHealthHistoryAsync(string appId, string appName, string resourceType, AppHealthInfo healthInfo);

        /// <summary>
        /// Get app health history document for a specific app
        /// </summary>
        /// <param name="appId">ID of the app or resource</param>
        /// <returns>App health history document or null if not found</returns>
        Task<AppHealthHistoryDocument?> GetAppHealthHistoryAsync(string appId);

        /// <summary>
        /// Prunes old data points from the health history array that are older than the specified time
        /// </summary>
        /// <param name="olderThan">Remove data points older than this datetime</param>
        /// <returns>Number of documents updated and total data points removed</returns>
        Task<(int DocumentsUpdated, int DataPointsRemoved)> PruneAppHealthHistoryAsync(DateTime olderThan);
    }
}
