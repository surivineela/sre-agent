// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins
{
    public interface IAppInsightsPlugin
    {
        Task<string> ExecuteAppInsightsQuery(string queryString); 
    }
}
