using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

// ThreadService will always take ThreadContext as a parameter.
public class ThreadService
{
    private readonly IThreadRepository _threadRepository;
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly ILogger<ThreadService> _logger;

    public ThreadService(
     ILogger<ThreadService> logger,
     IThreadRepository threadRepository,
     IThreadOrchestrationManager mappingManager)
    {
        _logger = logger;
        _threadRepository = threadRepository;
        _mappingManager = mappingManager;
    }

    /// <summary>
    /// Converts the thread messages to LLM chat history format, it just list user requests and agent response.
    /// Messages like SystemPrompt, PluginLog, or image content will be excluded.
    /// </summary>
    /// <returns>A list of chat messages in the format expected by the LLM.</returns>
    public async Task<List<AIChatMessage>> ToChatHistory(ThreadContext threadContext)
    {
        var ThreadMessages = await _threadRepository.GetMessagesAsync(threadContext.ThreadId);
        List<AIChatMessage> chatHistory = [];

        foreach (var msg in ThreadMessages)
        {
            // Skip messages with roles other than User, SREAgent, or System
            ChatRole role;
            switch (msg.Author.Role)
            {
                case Role.User:
                    role = ChatRole.User;
                    break;
                case Role.SREAgent:
                    role = ChatRole.Assistant;
                    break;
                default:
                    continue;  // Skip this message if role is PluginLog or not recognized
            }
            if (msg.IsImageContent)
            {
                continue;
            }
            chatHistory.Add(new AIChatMessage(role, msg.Text));
        }

        return chatHistory;
    }

    public async Task<List<AIChatMessage>> ToLLMChatHistory(ThreadContext threadContext, string systemPrompt)
    {
        List<AIChatMessage> chatHistory = [new AIChatMessage(ChatRole.System, systemPrompt)];
        chatHistory.AddRange(await ToChatHistory(threadContext));
        return chatHistory;
    }

    public async Task<string> GetLastUserMessage(ThreadContext threadContext)
    {
        var ThreadMessages = await _threadRepository.GetMessagesAsync(threadContext.ThreadId);
        var lastUserMessage = ThreadMessages.LastOrDefault(m => m.Author.Role == Role.User);
        return lastUserMessage?.Text ?? string.Empty;
    }
}