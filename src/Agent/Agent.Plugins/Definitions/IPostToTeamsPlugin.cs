using Microsoft.Bot.Schema;

namespace Agent.Plugins.Definitions
{
    public interface IPostToTeamsPlugin
    {
        Task<string> PostAsync(string message);
        Task<bool> PostTeamsMessage(string threadId, Activity message);
        Task<bool> PostToTeamsWithRetry(string message);
    }
}
