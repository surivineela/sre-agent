using Agent.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Agent.Web.Services
{
    public interface IChatHistoryStorage
    {
        Task<List<ChatMessage>> GetChatHistoryAsync(string threadId);
        Task AddMessageAsync(string threadId, ChatMessage message);
        Task<bool> ThreadExistsAsync(string threadId);
        Task<List<string>> GetThreadIdsAsync();
        Task<DateTime?> GetLastMessageTimestampAsync(string threadId);
    }

    public class ChatHistoryStorage : IChatHistoryStorage
    {
        private readonly Dictionary<string, List<ChatMessage>> _threadHistory = new();
        private readonly object _lock = new object();

        public Task<List<ChatMessage>> GetChatHistoryAsync(string threadId)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(threadId) || !_threadHistory.ContainsKey(threadId))
                {
                    return Task.FromResult(new List<ChatMessage>());
                }

                // Return a copy to prevent modification from outside
                var messages = _threadHistory[threadId];
                var result = messages.Select(m => new ChatMessage
                {
                    Message = m.Message,
                    IsUser = m.IsUser,
                    Timestamp = m.Timestamp
                }).ToList();

                return Task.FromResult(result);
            }
        }

        public Task AddMessageAsync(string threadId, ChatMessage message)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(threadId))
                {
                    throw new ArgumentException("ThreadId cannot be null or empty", nameof(threadId));
                }

                if (!_threadHistory.ContainsKey(threadId))
                {
                    _threadHistory[threadId] = new List<ChatMessage>();
                }

                _threadHistory[threadId].Add(message);
                return Task.CompletedTask;
            }
        }

        public Task<bool> ThreadExistsAsync(string threadId)
        {
            lock (_lock)
            {
                return Task.FromResult(!string.IsNullOrEmpty(threadId) && _threadHistory.ContainsKey(threadId));
            }
        }

        public Task<List<string>> GetThreadIdsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_threadHistory.Keys.ToList());
            }
        }

        public Task<DateTime?> GetLastMessageTimestampAsync(string threadId)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(threadId) || !_threadHistory.ContainsKey(threadId) || !_threadHistory[threadId].Any())
                {
                    return Task.FromResult<DateTime?>(null);
                }

                return Task.FromResult<DateTime?>(_threadHistory[threadId].Last().Timestamp);
            }
        }
    }
}