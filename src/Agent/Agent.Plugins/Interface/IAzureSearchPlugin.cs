// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Models;

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
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Search result containing TSG content</returns>
        Task<SearchResult> GetTsgContent(string searchText, CancellationToken cancellationToken = default);
    }
}
