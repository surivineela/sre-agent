// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;

namespace Agent.Framework;

/// <summary>
/// Handles streaming content extraction and processing for structured output types.
/// Encapsulates output type validation, streamable content property detection, and streaming JSON parsing.
/// </summary>
public class JsonStreamContentHandler : IStreamContentHandler
{
    private readonly Type _outputType;
    private readonly IDisplayModelOutput? _displayModelOutput;
    private readonly string? _streamableContentPropertyName;
    private readonly StreamExtractor? _streamExtractor;
    private bool _hasStreamedContent;

    /// <summary>
    /// Gets the output type associated with this handler.
    /// </summary>
    public Type OutputType => _outputType;

    /// <summary>
    /// Gets the name of the property marked with StreamableContentAttribute, if any.
    /// </summary>
    public string? StreamableContentPropertyName => _streamableContentPropertyName;

    /// <summary>
    /// Initializes a new instance of the JsonStreamContentHandler class.
    /// </summary>
    /// <param name="outputType">The output type containing property definitions.</param>
    /// <param name="displayModelOutput">Optional display model output handler invoked for each chunk of the property value.</param>
    public JsonStreamContentHandler(Type outputType, IDisplayModelOutput? displayModelOutput)
    {
        _outputType = outputType;
        _displayModelOutput = displayModelOutput;

        // Perform StreamableContentAttribute validation
        _streamableContentPropertyName = ExtractStreamableContentPropertyName(outputType);

        if (string.IsNullOrEmpty(_streamableContentPropertyName))
        {
            return;
        }

        // Initialize the stream extractor
        _streamExtractor = new StreamExtractor(_streamableContentPropertyName!, OnStreamContentAsync);
    }

    /// <summary>
    /// Extracts the property name marked with StreamableContentAttribute from the output type.
    /// </summary>
    /// <param name="outputType">The output type to inspect.</param>
    /// <returns>The name of the property marked with StreamableContentAttribute, or null if none found.</returns>
    private static string? ExtractStreamableContentPropertyName(Type outputType)
    {
        var property = outputType.GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<StreamableContentAttribute>() is not null);

        return property?.Name;
    }

    /// <summary>
    /// Callback invoked when streamable content is extracted by StreamExtractor.
    /// Outputs the content directly without buffering.
    /// </summary>
    /// <param name="content">The extracted content from the streamable property.</param>
    private async Task OnStreamContentAsync(string content)
    {
        if (_displayModelOutput is null)
        {
            return;
        }

        // Output directly without buffering
        await _displayModelOutput.OnDisplay(content);
    }

    /// <summary>
    /// Appends streaming content as it arrives. Implements IStreamContentHandler.AppendAsync.
    /// </summary>
    /// <param name="content">The content to append.</param>
    public async Task AppendAsync(string content)
    {
        _hasStreamedContent = true;
        if (_streamExtractor != null)
        {
            // Pass raw JSON content to the stream extractor
            // It will extract the streamable property value and call OnStreamContentAsync
            await _streamExtractor.AppendAsync(content);
        }
    }

    /// <summary>
    /// Called when streaming is complete. Implements IStreamContentHandler.CompleteAsync.
    /// </summary>
    /// <param name="finishReason">The reason why the streaming completed</param>
    public async Task CompleteAsync()
    {
        if (_displayModelOutput == null || !_hasStreamedContent)
        {
            return;
        }

        await _displayModelOutput.OnComplete();

        _hasStreamedContent = false; // reset
    }

    /// <summary>
    /// Called when streaming is incomplete. Implements IStreamContentHandler.IncompleteAsync.
    /// </summary>
    public async Task IncompleteAsync()
    {
        // Await OnIncomplete to ensure DB write completes
        if (_displayModelOutput != null)
        {
            await _displayModelOutput.OnIncomplete();
        }
    }
}
