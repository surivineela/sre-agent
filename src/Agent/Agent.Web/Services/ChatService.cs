namespace Agents.Web.Services;
using Agents.Core.Models;

public class ChatService : IChatService
{
    public async Task<ChatMessage> ProcessMessageAsync(string message)
    {
        await Task.Delay(500); // Simulate processing
        return new ChatMessage
        {
            Message = "This is a sample response from the AI assistant.",
            IsUser = false,
            Timestamp = DateTime.UtcNow
        };
    }
} 