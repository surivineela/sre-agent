// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Framework;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Microsoft.Bot.Schema.Teams;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Base class for plugin definitions that dynamically invoke tools from the tool factory.
    /// Provides methods to register and manage tools of a specific aggregate type.
    /// Automatically creates the plugin instance based on the AggregateType property of the derived class.
    /// </summary>
    public abstract class AggregateToolCallPluginDefinitionBase
    {
        protected readonly IAggregateToolCallPlugin _plugin;

        /// <summary>
        /// Gets the aggregate tool type that this plugin manages.
        /// For example, "KustoTool" for Kusto-related tools.
        /// </summary>
        protected abstract string AggregateType { get; }

        protected AggregateToolCallPluginDefinitionBase()
        {
            if (string.IsNullOrEmpty(AggregateType))
            {
                throw new InvalidOperationException(
                    $"Class {GetType().Name} must override AggregateType property with a non-empty value.");
            }

            _plugin = new AggregateToolCallPlugin(AggregateType);
        }        /// <summary>
                 /// Constructor that allows passing a custom plugin instance (for testing or advanced scenarios).
                 /// </summary>
        protected AggregateToolCallPluginDefinitionBase(IAggregateToolCallPlugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        /// <summary>
        /// Registers a tool that belongs to this aggregate type.
        /// This method should be called during tool registration in the ToolFactory.
        /// </summary>
        /// <typeparam name="TContext">The context type for the tool function.</typeparam>
        /// <param name="aggregateType">The aggregate type (e.g., "KustoTool").</param>
        /// <param name="toolName">The name of the tool to register.</param>
        /// <param name="toolFunction">The deferred tool function.</param>
        public void RegisterTool<TContext>(string aggregateType, string toolName, IDeferredToolFunction<TContext> toolFunction) where TContext : class
        {
            _plugin.RegisterTool(aggregateType, toolName, toolFunction);
        }

        /// <summary>
        /// Gets the aggregated description of all registered tools for this aggregate type.
        /// </summary>
        /// <returns>A formatted string containing descriptions of all registered tools.</returns>
        public string GetAggregatedDescription()
        {
            return _plugin.GetAggregatedDescription();
        }

        /// <summary>
        /// Calls a tool function through the aggregate plugin.
        /// </summary>
        /// <param name="methodName">The name of the tool/method to invoke.</param>
        /// <param name="arguments">JSON string containing the arguments as key-value pairs.</param>
        /// <returns>A JSON string containing the result or error information.</returns>
        protected async Task<string> CallToolFunctionAsync(string methodName, string arguments)
        {
            return await _plugin.CallToolFunctionAsync(methodName, arguments);
        }
    }
}
