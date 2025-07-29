// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Plugins.DataConnectors.TSG;
using Azure.Search.Documents.Models;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Interface for Azure Search operations
    /// </summary>
    public interface IAzureSearchPlugin
    {
        /// <summary>
        /// Retrieves TSG (Troubleshooting Guide) content based on search text
        /// </summary>
        /// <param name="searchText">Text to search for in the TSG content</param>
        /// <param name="maxResults">Maximum number of results to return (default: 5)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of TSG documents matching the search criteria</returns>
        Task<IReadOnlyList<TsgDocumentMetadata>> GetTsgContent(string searchText, int maxResults = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lookup related GitHub issues based on issue URL and descriptions
        /// Note: Returns generic objects to avoid cross-project dependencies
        /// </summary>
        /// <param name="issueUrl">The GitHub issue URL</param>
        /// <param name="issueDescriptions">List of issue descriptions to search for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of related GitHub issues as objects</returns>
        Task<IEnumerable<object>> LookupRelatedGitHubIssues(string issueUrl, List<string> issueDescriptions, CancellationToken cancellationToken = default);
    }
}
