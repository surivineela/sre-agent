// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Framework;
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
    public Task<SearchResult> GetTsgContent(
        [Description("Text to search for in the TSG content")] string searchText)
    {
        return _azureSearchPlugin.GetTsgContent(searchText, default);
    }
}
