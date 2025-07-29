// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.DataConnectors.TSG;
using Agent.Plugins.Interface;

namespace Agent.Plugins;

[AgentToolPlugin(Category = ToolCategories.KnowledgeBase)]
public class AzureSearchPluginDefinition
{
    private readonly IAzureSearchPlugin _azureSearchPlugin;

    public AzureSearchPluginDefinition(IAzureSearchPlugin plugin)
    {
        _azureSearchPlugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    
    [Description("Retrieve troubleshooting guide (TSG) content based on search text.")]
    public async Task<IReadOnlyList<TsgDocumentMetadata>> GetTsgContent(
        [Description("Text to search for in the TSG content")] string searchText,
        [Description("Maximum number of results to return (default: 5)")] int maxResults = 5)
    {
        return await _azureSearchPlugin.GetTsgContent(searchText, maxResults, default);
    }
}
