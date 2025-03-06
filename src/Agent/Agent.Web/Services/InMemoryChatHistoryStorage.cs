using Agent.Core.Models;
using System.Collections.Concurrent;

namespace Agent.Web.Services
{
    public class InMemoryChatHistoryStorage : IChatHistoryStorage
    {
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _threadHistory = new();
        private readonly ILogger<InMemoryChatHistoryStorage> _logger;

        public InMemoryChatHistoryStorage(ILogger<InMemoryChatHistoryStorage> logger)
        {
            _logger = logger;
        }

        public Task AddMessageAsync(string threadId, ChatMessage message)
        {
            if (string.IsNullOrEmpty(threadId))
            {
                _logger.LogWarning("Attempted to add message to empty threadId");
                return Task.CompletedTask;
            }

            // Ensure thread exists in dictionary
            _threadHistory.GetOrAdd(threadId, _ => new List<ChatMessage>());
            
            // Add message
            _threadHistory[threadId].Add(message);
            _logger.LogInformation($"Added message to thread {threadId}. Total messages: {_threadHistory[threadId].Count}");
            
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(string threadId, ChatMessage message, string agentType)
        {
            return AddMessageAsync(threadId, message);
        }

        public Task<List<ChatMessage>> GetChatHistoryAsync(string threadId, string? agentType = null)
        {
            if (string.IsNullOrEmpty(threadId) || !_threadHistory.ContainsKey(threadId))
            {
                _logger.LogInformation($"No history found for thread {threadId}");
                return Task.FromResult(new List<ChatMessage>());
            }

            // Return a copy to prevent outside modification
            var history = _threadHistory[threadId].Select(m => new ChatMessage
            {
                Message = m.Message,
                IsUser = m.IsUser,
                Timestamp = m.Timestamp
            }).ToList();

            _logger.LogInformation($"Retrieved {history.Count} messages for thread {threadId}");
            return Task.FromResult(history);
        }

        public Task<List<ChatMessage>> GetChatHistoryAsync(string threadId)
        {
            return Task.FromResult(_threadHistory[threadId]);
        }

        public Task<List<string>> GetChatIdsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<DateTime?> GetLastMessageTimestampAsync(string threadId)
        {
            if (string.IsNullOrEmpty(threadId) || !_threadHistory.ContainsKey(threadId))
            {
                return Task.FromResult<DateTime?>(null);
            }

            var messages = _threadHistory[threadId];
            if (!messages.Any())
            {
                return Task.FromResult<DateTime?>(null);
            }

            return Task.FromResult<DateTime?>(messages.Max(m => m.Timestamp));
        }

        public Task<List<string>> GetThreadIdsAsync()
        {
            return Task.FromResult(_threadHistory.Keys.ToList());
        }

        public Task<bool> ThreadExistsAsync(string threadId)
        {
            return Task.FromResult(_threadHistory[threadId] != null);
        }
    }
} 