// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Handles reasoning summaries
/// </summary>
public class ReasoningStreamContentHandler : IStreamContentHandler
{
    private readonly IDisplayModelOutput? _displayModelOutput;
    private bool _hasStreamedContent;

    /// <summary>
    /// Initializes a new instance of the TextStreamContentHandler class.
    /// </summary>
    /// <param name="displayModelOutput">Optional display model output handler invoked for each chunk of content.</param>
    public ReasoningStreamContentHandler(IDisplayModelOutput? displayModelOutput)
    {
        _displayModelOutput = displayModelOutput;
    }

    /// <summary>
    /// Appends streaming content as it arrives. Implements IStreamContentHandler.AppendAsync.
    /// </summary>
    /// <param name="content">The content to append.</param>
    public async Task AppendAsync(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        _hasStreamedContent = true;

        if (_displayModelOutput is null)
        {
            return;
        }

        // Await OnDisplay to ensure DB write completes
        await _displayModelOutput.OnDisplay(
            content: content,
            streamMessageType: StreamMessageType.Reasoning);
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

        await _displayModelOutput.OnComplete(streamMessageType: StreamMessageType.Reasoning);

        _hasStreamedContent = false; // reset
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
