using Microsoft.Extensions.AI;
using Agent.Core.Interfaces;
using Agent.Core.Extensions;

namespace Agent.Core.Helpers;

public static class TitleHelper
{
    /// <summary>
    /// Generates a concise title for a conversation based on the provided message
    /// </summary>
    /// <param name="chatClient">The chat client to use for title generation</param>
    /// <param name="message">The message content to generate a title from</param>
    /// <returns>A generated title string</returns>
    public static async Task<string> GenerateTitleAsync(
        IChatClient chatClient,
        string message)
    {
        // Input validation
        if (chatClient == null)
            throw new ArgumentNullException(nameof(chatClient));

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Conversation title...";
        }

        try
        {
            var chats = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "Generate a concise, descriptive title (maximum 6 words) for this conversation. Return only the title text without quotes or extra formatting."),
                new ChatMessage(ChatRole.User, message)
            };

            var response = await chatClient.GetResponseAsync(chats);
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
    /// <param name="chatClient">The chat client to generate the title</param>
    /// <param name="threadRepository">The repository to update the thread</param>
    /// <param name="threadId">The ID of the thread to update</param>
    /// <param name="message">The message content to generate a title from</param>
    public static async Task GenerateTitleAndUpdateAsync(
        IChatClient chatClient,
        IThreadRepository threadRepository,
        Guid threadId,
        string message)
    {
        // Generate the AI title
        string aiTitle = await GenerateTitleAsync(chatClient, message);
        // Update the thread title in the database
        await threadRepository.UpdateThreadTitleAsync(threadId, aiTitle);
    }
}