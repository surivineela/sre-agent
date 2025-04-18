// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Microsoft.AspNetCore.OData.Query;

namespace Agent.Core.Interfaces;

public interface IThreadRepository
{
    Task<Thread> GetThreadAsync(Guid threadId);
    Task<IEnumerable<Thread>> GetThreadsAsync(ODataQueryOptions? queryOptions = null);
    Task<IEnumerable<Thread>> GetThreadsBySourceAsync(ODataQueryOptions? queryOptions = null, ThreadSource? source = null);
    Task<Thread> CreateThreadAsync(Thread thread);
    Task<bool> DeleteThreadAsync(Guid threadId);

    Task<Thread> UpdateThreadTitleAsync(Guid threadId, string newTitle);

    Task<Message> GetMessageAsync(Guid threadId, Guid messageId);
    Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId, ODataQueryOptions? queryOptions = null);
    Task<Message> AddMessageAsync(Guid threadId, Message message);
    Task<bool> DeleteMessageAsync(Guid threadId, Guid messageId);

    Task<ThreadContext> GetThreadContextAsync(Guid threadId);
    Task<IEnumerable<ThreadContext>> GetThreadContextsAsync(ODataQueryOptions? queryOptions = null);
    Task<ThreadContext> AddThreadContextAsync(ThreadContext context);
    Task<ThreadContext> UpdateThreadContextAsync(ThreadContext context);
    Task<bool> DeleteThreadContextAsync(Guid threadId);

    Task<IEnumerable<Action>> GetActionsAsync(Guid threadId, ODataQueryOptions? queryOptions = null);
    Task<Action> GetActionAsync(Guid threadId, Guid actionId);
    Task<Action> AddActionAsync(Guid threadId, Action action);

    Task<MessageFeedback> GetMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId);
    Task<IEnumerable<MessageFeedback>> GetMessageFeedbacksAsync(Guid threadId, ODataQueryOptions? queryOptions = null);
    Task<MessageFeedback> AddOrUpdateMessageFeedbackAsync(Guid threadId, MessageFeedback messageFeedback);
    Task<bool> DeleteMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId);
    Task<MessageFeedback> GetMessageFeedbackNeedingRCAAsync();

    Task<AgentContext> GetAgentContextAsync(Guid agentContextId, Guid threadId);
    Task<IEnumerable<AgentContext>> GetAgentContextsForThreadAsync(Guid threadId);
    Task<AgentContext> CreateAgentContextAsync(AgentContext agentContext);
    Task<AgentContext> UpdateAgentContextAsync(AgentContext agentContext);
    Task<bool> DeleteAgentContextAsync(Guid agentContextId, Guid threadId);

    Task<ReasoningMessage> GetReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId);
    Task<ReasoningMessage> CreateReasoningMessageAsync(ReasoningMessage reasoningMessage);
    Task<bool> DeleteReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId);

    Task<AgentChatHistory> GetAgentChatHistoryAsync(Guid agentContextId);
    Task<AgentChatHistory> CreateAgentChatHistoryAsync(AgentChatHistory agentChatHistory);
    Task<AgentChatHistory> UpdateAgentChatHistoryAsync(AgentChatHistory agentChatHistory);
    Task<bool> DeleteAgentChatHistoryAsync(Guid agentContextId);
}
