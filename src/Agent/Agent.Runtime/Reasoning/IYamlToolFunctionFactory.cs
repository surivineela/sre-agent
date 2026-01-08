// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Factory interface for creating YamlToolFunction instances.
/// Centralizes the logic for resolving executor factories and creating tool functions.
/// </summary>
/// <typeparam name="TContext">The agent context type.</typeparam>
public interface IYamlToolFunctionFactory<TContext> where TContext : class
{
    /// <summary>
    /// Creates a YamlToolFunction for the given tool definition.
    /// </summary>
    /// <param name="toolDef">The YAML tool definition.</param>
    /// <returns>A YamlToolFunction wrapping the tool executor.</returns>
    YamlToolFunction<TContext> Create(YamlToolDefinitionBase toolDef);
}
