// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Microsoft.Bot.Schema.Teams;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Interface for the AggregateToolCall plugin that dynamically invokes tools from the tool factory.
    /// </summary>
    public interface IAggregateToolCallPlugin
    {
        /// <summary>
        /// Invokes a tool/method by name with the provided arguments.
        /// </summary>
        /// <param name="methodName">The name of the tool/method to invoke.</param>
        /// <param name="arguments">JSON string containing the arguments as key-value pairs.</param>
        /// <returns>A JSON string containing the result or error information.</returns>
        Task<string> CallToolFunctionAsync(string methodName, string arguments);

        /// <summary>
        /// Registers a tool with a specific aggregate type.
        /// This method should be called during tool registration in the ToolFactory.
        /// </summary>
        /// <typeparam name="TContext">The context type for the tool function.</typeparam>
        /// <param name="aggregateType">The aggregate type (e.g., "KustoTool").</param>
        /// <param name="toolName">The name of the tool to register.</param>
        /// <param name="toolFunction">The deferred tool function.</param>
        void RegisterTool<TContext>(string aggregateType, string toolName, IDeferredToolFunction<TContext> toolFunction) where TContext : class;

        /// <summary>
        /// Gets the aggregated description of all registered tools for this aggregate type.
        /// </summary>
        /// <returns>A formatted string containing descriptions of all registered tools.</returns>
        string GetAggregatedDescription();
    }
}
