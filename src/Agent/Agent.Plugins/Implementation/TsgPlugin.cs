// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.DataConnectors;
using Agent.Plugins.DataConnectors.TSG;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Plugins.Implementation
{
    /// <summary>
    /// Implementation of ITsgPlugin that provides TSG content retrieval using DataConnector
    /// </summary>
    public class TsgPlugin : ITsgPlugin
    {
        private readonly ILogger<TsgPlugin> _logger;
        private readonly DataConnectorIndex _dataConnectorIndex;

        public TsgPlugin(
            ILogger<TsgPlugin> logger, 
            DataConnectorIndex dataConnectorIndex)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataConnectorIndex = dataConnectorIndex ?? throw new ArgumentNullException(nameof(dataConnectorIndex));
        }

        /// <summary>
        /// Retrieves TSG content based on search text using the TSG DataConnector
        /// </summary>
        /// <param name="searchText">Text to search for in the TSG content</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of TSG documents matching the search criteria</returns>
        public async Task<IReadOnlyList<TsgDocumentMetadata>> GetTsgContent(
            string searchText, 
            int maxResults = 5, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation($"Retrieving TSG content for search text: {searchText}, maxResults: {maxResults}");

            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    throw new ArgumentException("Search text cannot be empty", nameof(searchText));
                }

                if (maxResults <= 0)
                {
                    throw new ArgumentException("Max results must be greater than 0", nameof(maxResults));
                }
                
                var results = new List<TsgDocumentMetadata>();

                // Use the DataConnectorIndex.SearchAsync method directly
                await foreach (var searchResult in _dataConnectorIndex.SearchAsync<TsgDocumentMetadata>(searchText, string.Empty, maxResults))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    results.Add(searchResult.OriginalDocument);
                }

                _logger.LogInternalInformation($"Found {results.Count} TSG documents for query: {searchText}");
                return results;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogInternalError(ex, $"Error retrieving TSG content for search text: {searchText}");
                throw;
            }
        }
    }
}