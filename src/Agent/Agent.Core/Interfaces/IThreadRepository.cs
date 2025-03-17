using Agent.Core.Models.Api.v1;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Core.Interfaces;

public interface IThreadRepository
{
    Task<Thread> GetThreadAsync(Guid threadId);
    Task<IEnumerable<Thread>> GetThreadsAsync(string? filter = null, int? skip = null, int? take = null);
    Task<Thread> CreateThreadAsync(Thread thread);
    Task<bool> DeleteThreadAsync(Guid threadId);

    Task<Message> GetMessageAsync(Guid threadId, Guid messageId);
    Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId, string? filter = null, int? skip = null, int? take = null);
    Task<Message> AddMessageAsync(Guid threadId, Message message);
    Task<bool> DeleteMessageAsync(Guid threadId, Guid messageId);

    Task<IEnumerable<Action>> GetActionsAsync(Guid threadId, int? skip = null, int? take = null);
    Task<Action> AddActionAsync(Guid threadId, Action action);
}
