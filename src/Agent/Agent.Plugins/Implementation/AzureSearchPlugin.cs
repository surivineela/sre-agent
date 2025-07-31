// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.DataConnectors;
using Agent.Plugins.DataConnectors.TSG;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    /// <summary>
    /// Implementation of IAzureSearchPlugin that provides TSG content retrieval
    /// using the TSG DataConnector and Azure Cognitive Search
    /// </summary>
    public class AzureSearchPlugin : IAzureSearchPlugin
    {
        private readonly ILogger<AzureSearchPlugin> _logger;
        private readonly DataConnectorIndex _dataConnectorIndex;

        public AzureSearchPlugin(
            ILogger<AzureSearchPlugin> logger, 
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

        /// <summary>
        /// Lookup related GitHub issues based on issue URL and descriptions
        /// </summary>
        /// <param name="issueUrl">The GitHub issue URL</param>
        /// <param name="issueDescriptions">List of issue descriptions to search for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of related GitHub issues</returns>
        public async Task<IEnumerable<object>> LookupRelatedGitHubIssues(
            string issueUrl, 
            List<string> issueDescriptions, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation($"Looking up related GitHub issues for: {issueUrl}");

            try
            {
                // TODO: Implement GitHub issue search functionality
                // This should search through indexed GitHub issues and return related ones
                // For now, return empty list as placeholder

                await Task.CompletedTask; // Placeholder to avoid compiler warning
                return new List<object>();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogInternalError(ex, $"Error looking up related GitHub issues for: {issueUrl}");
                throw;
            }
        }
    }
}
