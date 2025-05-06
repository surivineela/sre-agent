// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Plugins
{
    /// <summary>
    /// Print Kusto queries in chat messages
    /// </summary>
    public interface IKustoPluginChat : IKustoPlugin
    {
        public Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args);
    }
}
