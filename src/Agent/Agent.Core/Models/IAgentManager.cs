namespace Agent.Core.Models
{
    public interface IAgentManager : IDisposable
    {
        Task<string> StartChatThread(string path, string threadId);
        Task<ChatMessage> TrackChatThread(string threadId, string message);
        IEnumerable<string> GetAvailableSubAgents();
        List<ChatThreadInfo> GetChatThreads();
    }

    public class ChatThreadInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string AgentType { get; set; } = "";
    }
}
