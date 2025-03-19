// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Core.Services;

public interface IChatService
{
    IAsyncEnumerable<string> ProcessMessageStreamAsync(string message, string chatId, CancellationToken cancellationToken = default);
    Task<ChatMessage> ProcessMessageAsync(string message, string? chatId);
    Task<List<ChatMessage>> GetChatHistoryAsync(string chatId);
    Task SwitchAgent(string path, string chatId);  // SwitchAgent to switch between chat threads and agent type by path.
    Task<string> StartThreadAsync(string path, string chatId);
    Task<List<ChatThread>> GetThreadsAsync();
    Task SetThreadAsync(string chatId);
    Task<string?> GetCurrentChatIdAsync();
}

public record ChatThread
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AgentType { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
}
