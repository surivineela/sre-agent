// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Common;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Service for processing tool outputs, including truncation and storage of large outputs.
/// Uses a processor factory to delegate type-specific processing.
/// </summary>
public class ToolOutputProcessService : IToolOutputProcessService
{
    private readonly IThreadFileStorageService _threadFileStorageService;
    private readonly ILogger<ToolOutputProcessService> _logger;
    private readonly IToolOutputProcessorFactory _processorFactory;
    private readonly DefaultToolOutputProcessor _defaultProcessor;
    private readonly int _maxCharacterCount;

    public ToolOutputProcessService(
        IThreadFileStorageService threadFileStorageService,
        ILogger<ToolOutputProcessService> logger,
        IOptions<ToolOutputSettings> settings,
        IToolOutputProcessorFactory? processorFactory = null)
    {
        _threadFileStorageService = threadFileStorageService;
        _logger = logger;
        _maxCharacterCount = settings.Value.MaxOutputChars;
        _processorFactory = processorFactory ?? new ToolOutputProcessorFactory();
        _defaultProcessor = new DefaultToolOutputProcessor(_maxCharacterCount, logger);
    }

    /// <summary>
    /// Determines if the output should be truncated based on size thresholds
    /// </summary>
    /// <param name="output">The tool output to check</param>
    /// <returns>True if the output exceeds size thresholds</returns>
    public bool ShouldTruncate(string? output)
    {
        return _defaultProcessor.ShouldTruncate(output);
    }

    /// <summary>
    /// Processes tool output, truncating and storing large outputs
    /// </summary>
    /// <param name="threadId">The thread ID for this execution</param>
    /// <param name="tool">The AIFunction that produced this output</param>
    /// <param name="output">The original tool output (string or object that will be serialized to JSON)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The processed output - either the original if small enough,
    /// or a truncation message with file reference if large
    /// </returns>
    public async Task<object?> ProcessToolOutputAsync(
        Guid threadId,
        AIFunction tool,
        object? output,
        CancellationToken cancellationToken = default)
    {
        if (output == null)
        {
            return output;
        }

        // Check if the tool has DisableOutputTruncation attribute set
        if (tool.ShouldDisableOutputTruncation())
        {
            _logger.LogInternalInformation("Returning full output for tool {ToolName} (DisableOutputTruncation=true)", tool.Name);
            return output;
        }

        // Delegate to the string-based implementation
        return await ProcessToolOutputAsync(threadId, tool.Name, output, cancellationToken);
    }

    /// <summary>
    /// Processes tool output using a generic context, truncating and storing large outputs
    /// </summary>
    public async Task<object?> ProcessToolOutputAsync<TContext>(
        TContext? context,
        AIFunction tool,
        object? output,
        CancellationToken cancellationToken = default) where TContext : class
    {
        // Extract ThreadId from context using reflection
        var threadIdProperty = typeof(TContext).GetProperty("ThreadId");
        if (threadIdProperty == null)
        {
            _logger.LogInternalWarning("Context type {ContextType} does not have a ThreadId property", typeof(TContext).Name);
            return output;
        }

        var threadIdValue = threadIdProperty.GetValue(context);
        if (threadIdValue is not Guid threadId)
        {
            _logger.LogInternalWarning("ThreadId property in context is not a Guid");
            return output;
        }

        // Delegate to the main implementation
        return await ProcessToolOutputAsync(threadId, tool, output, cancellationToken);
    }

    /// <summary>
    /// Processes tool output, truncating and storing large outputs (string toolName overload)
    /// </summary>
    public async Task<object?> ProcessToolOutputAsync(
        Guid threadId,
        string toolName,
        object? output,
        CancellationToken cancellationToken = default)
    {
        if (output == null)
        {
            return output;
        }

        // Create context for the processor
        var context = new ToolOutputProcessorContext
        {
            ThreadId = threadId,
            ToolName = toolName,
            SaveOutput = _threadFileStorageService.SaveToolOutputAsync
        };

        // First, try to get a type-specific processor
        var processor = _processorFactory.GetProcessorForType(output.GetType());
        if (processor != null)
        {
            // Use the type-specific processor - it handles all processing including storage
            return await processor.ProcessAsync(output, context, cancellationToken);
        }

        // No dedicated processor - use the default processor
        return await _defaultProcessor.ProcessAsync(output, context, cancellationToken);
    }

    /// <summary>
    /// Formats the truncation message that replaces the original output.
    /// Delegates to the default processor for consistent formatting.
    /// </summary>
    public string FormatTruncationMessage(string fileKey, string contentType, int lineCount, string originalOutput)
    {
        return _defaultProcessor.FormatTruncationMessage(fileKey, contentType, lineCount, originalOutput);
    }
}
