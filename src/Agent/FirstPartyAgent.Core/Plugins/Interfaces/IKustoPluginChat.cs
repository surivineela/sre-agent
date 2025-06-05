// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Plugins
{
    /// <summary>
    /// Print Kusto queries in chat messages
    /// </summary>
    public interface IKustoPluginChat : IACAKustoPlugin
    {
        public Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args, string groupName = "ContainerApps", SamplingOptions samplingOptions = null);
        public Task<string> ExecuteLocalFunctionOnClusterAsync(string functionName, string clusterName, string databaseName, Dictionary<string, string> args);
    }
}
