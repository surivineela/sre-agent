using Agents.Core.Models;

namespace Agents.Web.Services;


public interface IChatService
{
    Task<ChatMessage> ProcessMessageAsync(string message);
} 