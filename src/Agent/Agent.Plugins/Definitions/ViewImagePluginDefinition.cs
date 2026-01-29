// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(Category = ToolCategories.System)]
public class ViewImagePluginDefinition
{
    [Description(
        @"View an image file from the thread storage. Use this tool when you need to visually inspect an image 
        that was previously uploaded or generated in this thread. The image will be loaded and displayed for analysis.")]
    [AgentTool(ToolMode.Manual)] // requires special handling outside the framework
    public string ViewImage(
        [Description("The name of the image file to view from the thread storage.")]
        string fileName)
    {
        throw new InvalidOperationException("ViewImage is not expected to be called directly");
    }
}
