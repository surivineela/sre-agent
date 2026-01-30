// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework;

/// <summary>
/// Handles reasoning summaries for Anthropic models by generating titles using the ReasoningFast model.
/// Anthropic models don't emit **title** headers in their reasoning like GPT-5 models do,
/// so this handler generates titles and prepends them in the expected format.
/// </summary>
public class AnthropicReasoningStreamContentHandler : IStreamContentHandler
{
    private readonly IDisplayModelOutput? _displayModelOutput;
    private readonly IChatClient _titleGenerationClient;
    private readonly ILogger _logger;
    private bool _titleGenerated;
    private bool _hasStreamedContent;
    private readonly StringBuilder _initialBuffer = new();

    private const int MinCharsForTitleGeneration = 50;
    private const int MaxCharsForTitleGeneration = 500;
    private static readonly TimeSpan TitleGenerationTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Initializes a new instance of the AnthropicReasoningStreamContentHandler class.
    /// </summary>
    /// <param name="displayModelOutput">Display model output handler invoked for each chunk of content.</param>
    /// <param name="titleGenerationClient">The chat client (ReasoningFast model) used to generate titles.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public AnthropicReasoningStreamContentHandler(
        IDisplayModelOutput? displayModelOutput,
        IChatClient titleGenerationClient,
        ILogger logger)
    {
        _displayModelOutput = displayModelOutput;
        _titleGenerationClient = titleGenerationClient ?? throw new ArgumentNullException(nameof(titleGenerationClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Appends streaming content as it arrives. Buffers initial content to generate a title.
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

        if (!_titleGenerated)
        {
            _initialBuffer.Append(content);

            // Wait until we have enough content to generate a meaningful title
            if (_initialBuffer.Length < MinCharsForTitleGeneration)
            {
                return; // Buffer more content
            }

            // Generate title and flush buffer
            await GenerateTitleAndFlushAsync();
        }
        else
        {
            // Title already generated, pass through directly
            await _displayModelOutput.OnDisplay(
                content: content,
                streamMessageType: StreamMessageType.Reasoning);
        }
    }

    /// <summary>
    /// Generates a title from the buffered content and flushes it to the display.
    /// </summary>
    private async Task GenerateTitleAndFlushAsync()
    {
        _titleGenerated = true;
        var bufferedContent = _initialBuffer.ToString();
        _initialBuffer.Clear();

        var contentForTitle = bufferedContent.Substring(0, Math.Min(bufferedContent.Length, MaxCharsForTitleGeneration));
        var title = await GenerateTitleAsync(contentForTitle);

        // Prepend title in **title** format
        var contentWithTitle = string.IsNullOrEmpty(title)
            ? bufferedContent
            : $"**{title}**\n\n{bufferedContent}";

        await _displayModelOutput!.OnDisplay(
            content: contentWithTitle,
            streamMessageType: StreamMessageType.Reasoning);
    }

    /// <summary>
    /// Generates a concise title for the reasoning content using the ReasoningFast model.
    /// </summary>
    /// <param name="reasoningContent">The reasoning content to generate a title from.</param>
    /// <returns>A generated title, or fallback if generation fails.</returns>
    private async Task<string> GenerateTitleAsync(string reasoningContent)
    {
        try
        {
            using var cts = new CancellationTokenSource(TitleGenerationTimeout);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, GetTitleGenerationPrompt()),
                new(ChatRole.User, reasoningContent)
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = 50 // Title should be 3-6 words, 50 tokens is more than enough
            };

            var response = await _titleGenerationClient.GetResponseAsync(messages, options, cts.Token);
            var title = response.Messages.FirstOrDefault()?.Text?.Trim() ?? "";

            // Validate and sanitize
            if (string.IsNullOrWhiteSpace(title) || title.Length > 100)
            {
                return GetFallbackTitle();
            }

            // Remove any ** markers if the model added them
            title = title.Replace("**", "").Trim();

            return title;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Reasoning title generation timed out, using fallback title");
            return GetFallbackTitle();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate reasoning title, using fallback");
            return GetFallbackTitle();
        }
    }

    /// <summary>
    /// Gets the system prompt for title generation.
    /// </summary>
    private static string GetTitleGenerationPrompt()
    {
        return @"Generate a concise title (3-6 words) that summarizes what the AI model is thinking about based on this reasoning content. The title should capture the main focus or action being considered.

Rules:
- Return ONLY the title text, no quotes or formatting
- Use present participle form (e.g., ""Analyzing resource configuration"", ""Investigating error patterns"")
- Keep it under 50 characters
- Focus on the action/analysis being performed

Examples of good titles:
- Analyzing VM connectivity issues
- Investigating deployment failures
- Reviewing security configurations
- Examining log patterns";
    }

    /// <summary>
    /// Returns a fallback title when generation fails.
    /// </summary>
    private static string GetFallbackTitle()
    {
        return "Thinking";
    }

    /// <summary>
    /// Called when streaming is complete.
    /// </summary>
    public async Task CompleteAsync()
    {
        // Flush any remaining buffered content if title wasn't generated yet
        if (!_titleGenerated && _initialBuffer.Length > 0)
        {
            await GenerateTitleAndFlushAsync();
        }

        if (_displayModelOutput == null || !_hasStreamedContent)
        {
            return;
        }

        await _displayModelOutput.OnComplete(streamMessageType: StreamMessageType.Reasoning);

        _hasStreamedContent = false; // reset
    }

    /// <summary>
    /// Called when streaming is incomplete.
    /// </summary>
    public async Task IncompleteAsync()
    {
        // Flush buffer on incomplete too, but without title generation to avoid delay
        if (!_titleGenerated && _initialBuffer.Length > 0)
        {
            _titleGenerated = true;
            var content = _initialBuffer.ToString();
            _initialBuffer.Clear();

            if (_displayModelOutput != null)
            {
                await _displayModelOutput.OnDisplay(
                    content: content,
                    streamMessageType: StreamMessageType.Reasoning);
            }
        }

        if (_displayModelOutput != null)
        {
            await _displayModelOutput.OnIncomplete();
        }
    }
}
