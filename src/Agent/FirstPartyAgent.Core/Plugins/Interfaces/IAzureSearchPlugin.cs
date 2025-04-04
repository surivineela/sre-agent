// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;

namespace FirstPartyAgent.Core.Plugins
{
    public interface IAzureSearchPlugin
    {
        public Task<IEnumerable<SearchResult<SearchDocument>>> PerformSemanticSearchAsync(string searchText, CancellationToken cancellationToken = default);
    }
}

