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
    }

    /// <summary>
    /// Appends streaming content as it arrives. Implements IStreamContentHandler.AppendAsync.
    /// </summary>
    /// <param name="content">The content to append.</param>
    public async Task AppendAsync(string content)
    {
        _hasStreamedContent = true;

        if (_displayModelOutput is null)
        {
            return;
        }

        // Await OnDisplay to ensure DB write completes
        await _displayModelOutput.OnDisplay(content);
    }

    /// <summary>
    /// Called when streaming is complete. Implements IStreamContentHandler.CompleteAsync.
    /// </summary>
    /// <param name="finishReason">The reason why the streaming completed</param>
    public async Task CompleteAsync(ChatFinishReason? finishReason)
    {
        if (_displayModelOutput == null || !_hasStreamedContent)
        {
            return;
        }

        await _displayModelOutput.OnComplete(string.Empty, finishReason);
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
