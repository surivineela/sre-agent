// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Web.Services;

public interface IChatService
{
    Task<ChatMessage> ProcessMessageAsync(string message, string? threadId);
    Task<List<ChatMessage>> GetChatHistoryAsync(string threadId);
    Task SwitchAgent(string path, string threadId);  // SwitchAgent to switch between chat threads and agent type by path.
    Task<string> CreateThreadAsync(string path, string threadId);
    Task<List<ChatThread>> GetThreadsAsync();
    Task SetThreadAsync(string threadId);
    Task<string?> GetCurrentThreadIdAsync();
}

public class ChatThread
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AgentType { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
}
