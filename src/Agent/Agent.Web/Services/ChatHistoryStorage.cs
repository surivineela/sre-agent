using Agent.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agent.Web.Services
{
    public interface IChatHistoryStorage
    {
        Task<List<ChatMessage>> GetChatHistoryAsync(string chatId, string? agentType = null);
        Task AddMessageAsync(string chatId, ChatMessage message, string agentType);
        Task<bool> ThreadExistsAsync(string chatId);
        Task<List<string>> GetChatIdsAsync();
        Task<DateTime?> GetLastMessageTimestampAsync(string chatId);
    }

    public class ChatHistoryStorage : IChatHistoryStorage
    {
        private readonly Dictionary<string, List<ChatMessageWithAgent>> _threadHistory = new();
        private readonly object _lock = new object();

        // Private class to track messages with their associated agent
        private class ChatMessageWithAgent
        {
            public ChatMessage Message { get; set; }
            public string AgentType { get; set; }
        }

        public Task<List<ChatMessage>> GetChatHistoryAsync(string chatId, string? agentType = null)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(chatId) || !_threadHistory.ContainsKey(chatId))
                {
                    return Task.FromResult(new List<ChatMessage>());
                }

                var messages = _threadHistory[chatId];

                // If Meta agent is requested, return all messages from Meta agent only
                if (string.IsNullOrEmpty(agentType) || agentType.ToLower() == "meta")
                {
                    var result = messages
                        .Where(m => m.AgentType.ToLower() == "meta")
                        .Select(m => new ChatMessage
                        {
                            Message = m.Message.Message,
                            IsUser = m.Message.IsUser,
                            Timestamp = m.Message.Timestamp
                        }).ToList();

                    return Task.FromResult(result);
                }

                // For specific agent types, only return messages with that specific agent type
                var filteredMessages = messages
                    .Where(m => m.AgentType.ToLower() == agentType.ToLower())
                    .Select(m => new ChatMessage
                    {
                        Message = m.Message.Message,
                        IsUser = m.Message.IsUser,
                        Timestamp = m.Message.Timestamp
                    }).ToList();

                return Task.FromResult(filteredMessages);
            }
        }

        public Task AddMessageAsync(string chatId, ChatMessage message, string agentType)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(chatId))
                {
                    throw new ArgumentException("ChatId cannot be null or empty", nameof(chatId));
                }

                if (!_threadHistory.ContainsKey(chatId))
                {
                    _threadHistory[chatId] = new List<ChatMessageWithAgent>();
                }

                _threadHistory[chatId].Add(new ChatMessageWithAgent
                {
                    Message = message,
                    AgentType = agentType
                });

                return Task.CompletedTask;
            }
        }

        public Task<bool> ThreadExistsAsync(string chatId)
        {
            lock (_lock)
            {
                return Task.FromResult(!string.IsNullOrEmpty(chatId) && _threadHistory.ContainsKey(chatId));
            }
        }

        public Task<List<string>> GetChatIdsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_threadHistory.Keys.ToList());
            }
        }

        public Task<DateTime?> GetLastMessageTimestampAsync(string chatId)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(chatId) || !_threadHistory.ContainsKey(chatId) || !_threadHistory[chatId].Any())
                {
                    return Task.FromResult<DateTime?>(null);
                }

                return Task.FromResult<DateTime?>(_threadHistory[chatId].Last().Message.Timestamp);
            }
        }
    }
}