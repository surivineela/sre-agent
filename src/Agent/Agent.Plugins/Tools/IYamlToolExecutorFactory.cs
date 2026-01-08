// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Plugins.Tools;

/// <summary>
/// Factory interface for creating YAML tool executor instances.
/// Factories should be decorated with [ToolType] attribute to indicate which tool type they handle.
/// </summary>
public interface IYamlToolExecutorFactory
{
    /// <summary>
    /// Creates a tool executor instance for the given definition.
    /// </summary>
    /// <param name="definition">The YAML tool definition</param>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <returns>A concrete tool instance implementing IYamlToolExecutor</returns>
    IYamlToolExecutor Create(YamlToolDefinitionBase definition, IServiceProvider serviceProvider);
}
