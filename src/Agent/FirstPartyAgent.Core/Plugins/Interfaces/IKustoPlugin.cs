// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public interface IKustoPlugin
    {
        public Task<string> ExecuteKustoQuery(string region, string query);
        public Task<string> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery, DateTime? NowOverride, Kernel kernel);
    }
}
