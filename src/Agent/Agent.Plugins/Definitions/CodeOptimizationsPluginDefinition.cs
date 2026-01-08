// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins
{
    [AgentToolPlugin(Category = ToolCategories.Diagnostics)]
    public class CodeOptimizationsPluginDefinition
    {
        private readonly ICodeOptimizationsPlugin _plugin;

        public CodeOptimizationsPluginDefinition(ICodeOptimizationsPlugin plugin)
        {
            _plugin = plugin;
        }

        [Description("Returns code optimization insights for a list of app insights resources.")]
        [AgentTool(ToolMode.Auto)]
        public Task<Dictionary<string, IEnumerable<InsightsRecommendationContract>>> GetCodeOptimizationInsights(IEnumerable<string> resourceIds)
        {
            return _plugin.GetCodeOptimizationInsightsBulkAsync(resourceIds);
        }
    }
}
