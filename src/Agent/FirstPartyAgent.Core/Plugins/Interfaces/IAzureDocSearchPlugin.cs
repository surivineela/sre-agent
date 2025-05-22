// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;
using FirstPartyAgent.Core.Models;

namespace FirstPartyAgent.Core.Plugins
{
    public interface IAzureDocSearchPlugin
    {
        public Task<string> SearchDesignDocsAsync(string query);
    }
}

