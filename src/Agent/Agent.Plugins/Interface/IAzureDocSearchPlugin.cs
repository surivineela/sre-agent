// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface
{
    public interface IAzureDocSearchPlugin
    {
        public Task<string> SearchDesignDocsAsync(string query);
    }
}

