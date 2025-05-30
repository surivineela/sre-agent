// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using Agent.Core.Interfaces;
using Agent.Core.Extensions;

namespace Agent.Core.Helpers;

public class TitleGenerationService : ITitleGenerationService
{
    private readonly IChatClient _chatClient;
    private readonly IThreadRepository _threadRepository;
    public TitleGenerationService(
        IChatClient chatClient,
        IThreadRepository threadRepository)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
    }

    public virtual string GetTitleGenerationSystemPrompt()
    {
        return "This is a thread for Azure SRE Agent. Generate a concise, descriptive title (maximum 6 words) for this conversation. Return only the title text without quotes or extra formatting.";
    }

    /// <summary>
    /// Generates a concise title for a conversation based on the provided message
    /// </summary>
    /// <param name="message">The message content to generate a title from</param>
    /// <returns>A generated title string</returns>
    public async Task<string> GenerateTitleAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return CreateFallbackTitle();
        }

        try
        {
            var chats = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, GetTitleGenerationSystemPrompt()),
                new ChatMessage(ChatRole.User, message)
            };

            var response = await _chatClient.GetResponseAsync(chats);
            string title = response.GetMessage().Text?.Trim() ?? "";

            // Validate the response
            if (string.IsNullOrWhiteSpace(title))
            {
                return CreateFallbackTitle();
            }

            return title;
        }
        catch (Exception)
        {
            return CreateFallbackTitle();
        }
    }

    /// <summary>
    /// Creates a fallback title when the LLM-based generation fails
    /// </summary>
    private static string CreateFallbackTitle()
    {
        return "Conversation title...";
    }

    /// <summary>
    /// Background task to generate a better title and update the thread
    /// </summary>
    /// <param name="threadId">The ID of the thread to update</param>
    /// <param name="message">The message content to generate a title from</param>
    public async Task GenerateTitleAndUpdateThreadAsync(Guid threadId, string message)
    {
        // Generate the AI title
        string aiTitle = await GenerateTitleAsync(message);
        // Update the thread title in the database
        await _threadRepository.UpdateThreadTitleAsync(threadId, aiTitle);
    }
}
