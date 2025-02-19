// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Plugins
{
    public interface IKustoPlugin
    {
        public Task<string> ExecuteKustoQuery(string region, string query);
    }
}
