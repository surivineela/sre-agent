// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Plugin definition for dynamically invoking Kusto tools from the tool factory.
    /// </summary>
    [AgentToolPlugin(IsEnabled = true)]
    public class AggregatedKustoToolsPluginDefinition : AggregateToolCallPluginDefinitionBase
    {
        protected override string AggregateType => "KustoTool";

        [Description("")]
        public async Task<string> AggregatedKustoTool(
            [Description("The name of the Kusto tool/method to invoke.")] string methodName,
            [Description("JSON string containing the arguments as key-value pairs.")] string arguments)
        {
            return await CallToolFunctionAsync(methodName, arguments);
        }
    }
}
