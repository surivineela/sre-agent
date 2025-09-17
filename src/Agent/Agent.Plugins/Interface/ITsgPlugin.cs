// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Plugins.DataConnectors.TSG;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Interface for TSG (Troubleshooting Guide) operations
    /// </summary>
    public interface ITsgPlugin
    {
        /// <summary>
        /// Retrieves TSG content based on search text using DataConnector
        /// </summary>
        /// <param name="searchText">Text to search for in the TSG content</param>
        /// <param name="maxResults">Maximum number of results to return (default: 5)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of TSG documents matching the search criteria</returns>
        Task<IReadOnlyList<TsgDocumentMetadata>> GetTsgContent(string searchText, int maxResults = 5, CancellationToken cancellationToken = default);
    }
}