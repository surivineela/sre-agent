// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>
/// Handles simple text streaming content without structured output type processing.
/// Uses a content cache to accumulate streamed text.
/// </summary>
public class TextStreamContentHandler : IStreamContentHandler
{
    private readonly IDisplayModelOutput? _displayModelOutput;
    private System.Text.StringBuilder? _contentCache;
    private bool _hasStreamedContent;

    /// <summary>
    /// Gets the display model output handler.
    /// </summary>
    public IDisplayModelOutput? DisplayModelOutput => _displayModelOutput;

    /// <summary>
    /// Initializes a new instance of the TextStreamContentHandler class.
    /// </summary>
    /// <param name="displayModelOutput">Optional display model output handler invoked for each chunk of content.</param>
    public TextStreamContentHandler(IDisplayModelOutput? displayModelOutput)
    {
        _displayModelOutput = displayModelOutput;

        if (_displayModelOutput != null)
        {
            _contentCache = new System.Text.StringBuilder();
        }
    }

    /// <summary>
    /// Appends streaming content as it arrives. Implements IStreamContentHandler.AppendAsync.
    /// </summary>
    /// <param name="content">The content to append.</param>
    public async Task AppendAsync(string content)
    {
        _hasStreamedContent = true;

        if (_contentCache is null || _displayModelOutput is null)
        {
            return;
        }

        _contentCache.Append(content);

        // Check if cache has reached threshold and flush if needed
        if (_contentCache.Length >= IStreamContentHandler.ContentCacheThreshold)
        {
            var contentToDisplay = _contentCache.ToString();
            _contentCache.Clear();

            // Await OnDisplay to ensure DB write completes
            await _displayModelOutput.OnDisplay(contentToDisplay);
        }
    }

    /// <summary>
    /// Called when streaming is complete. Implements IStreamContentHandler.CompleteAsync.
    /// </summary>
    /// <param name="finishReason">The reason why the streaming completed</param>
    public async Task CompleteAsync(ChatFinishReason? finishReason)
    {
        if (_displayModelOutput == null || !_hasStreamedContent || _contentCache == null)
        {
            return;
        }

        var contentToDisplay = _contentCache.ToString();
        _contentCache.Clear();
        await _displayModelOutput.OnComplete(contentToDisplay, finishReason);
    }

    /// <summary>
    /// Called when streaming is incomplete. Implements IStreamContentHandler.IncompleteAsync.
    /// </summary>
    public async Task IncompleteAsync()
    {
        if (_displayModelOutput != null)
        {
            await _displayModelOutput.OnIncomplete();
        }
    }
}
