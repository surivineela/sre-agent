// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.DataConnectors.TSG;
using Agent.Plugins.Interface;

namespace Agent.Plugins;

[AgentToolPlugin(Category = ToolCategories.KnowledgeBase)]
public class TsgPluginDefinition
{
    private readonly ITsgPlugin _tsgPlugin;

    public TsgPluginDefinition(ITsgPlugin plugin)
    {
        _tsgPlugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Retrieve troubleshooting guide (TSG) content based on search text.")]
    public async Task<IReadOnlyList<TsgDocumentMetadata>> GetTsgContent(
        [Description("Text to search for in the TSG content")] string searchText,
        [Description("Maximum number of results to return (default: 5)")] int maxResults = 5)
    {
        return await _tsgPlugin.GetTsgContent(searchText, maxResults, default);
    }
}
