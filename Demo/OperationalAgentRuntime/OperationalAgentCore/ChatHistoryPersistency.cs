using Microsoft.SemanticKernel.ChatCompletion;
using OperationalAgentCore.Models;
using System.Text.Json;

namespace OperationalAgentCore;

public static class ChatHistoryPersistency
{
    private static readonly AsyncReaderWriterLock _lock = new();
    private static readonly ChatHistory s_chatHistory;

    static ChatHistoryPersistency()
    {
        s_chatHistory = new ChatHistory();
        string agentModeStr = Environment.GetEnvironmentVariable("AgentMode") ?? string.Empty;
        var agentMode = Enum.TryParse<AgentMode>(agentModeStr, out var mode) ? mode : AgentMode.SREAgent;
        SetSystemPrompt(agentMode);
    }

    private static void SetSystemPrompt(AgentMode agentMode)
    {
        string systemPrompt = agentMode == AgentMode.ICM ? ICMAgent.SystemMessage : IssueFinderAgent.SystemMessage;
        s_chatHistory.AddSystemMessage(systemPrompt);
    }

    public static async Task<T> ChatHistoryTransition<T>(
        Func<ChatHistory, Task<T>> action)
    {
        // TODO: is chathistory thread safe? what happens if two model request was send upon the same chat history
        using var _ = await _lock.AcquireWriterAsync();

        return await action(s_chatHistory);
    }
}
