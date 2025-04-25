// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.Logging;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Data.Repositories
{
    /// <summary>
    /// In-memory implementation of the IThreadRepository interface.
    /// This is primarily for testing purposes.
    /// </summary>
    public class InmemoryThreadRepository : IThreadRepository
    {
        private readonly Dictionary<Guid, Thread> _threads = new();
        private readonly Dictionary<Guid, ThreadContext> _threadContexts = new();
        private readonly Dictionary<(Guid ThreadId, Guid MessageId), Message> _messages = new();
        private readonly Dictionary<(Guid ThreadId, Guid MessageFeebackId), MessageFeedback> _messageFeedbacks = new();
        private readonly Dictionary<(Guid ThreadId, Guid ActionId), Action> _actions = new();
        private readonly Dictionary<(Guid ThreadId, Guid AgentContextId), AgentContext> _agentContexts = new();
        private readonly Dictionary<(Guid AgentContextId, Guid ReasoningMessageId), ReasoningMessage> _reasoningMessages = new();
        private readonly Dictionary<(Guid ThreadId, Guid ApprovalId), Approval> _approvals = new();
        private readonly Dictionary<(Guid AgentContextId, Guid ApprovalV2Id), ApprovalV2> _approvalv2s = new();
        private readonly Dictionary<Guid, AgentChatHistory> _agentChatHistories = new();
        private readonly Dictionary<string, object> _threadTeamsMappings = new();
        private readonly ILogger<InmemoryThreadRepository> _logger;

        public InmemoryThreadRepository(ILogger<InmemoryThreadRepository> logger)
        {
            _logger = logger;
        }

        #region Thread Operations

        public Task<Thread> GetThreadAsync(Guid threadId)
        {
            _logger.LogInformation("Trying to get thread: {Id}", threadId);
            _threads.TryGetValue(threadId, out var thread);
            return Task.FromResult(thread);
        }

        public Task<IEnumerable<Thread>> GetThreadsAsync(ODataQueryOptions? queryOptions)
        {
            IQueryable<Thread> threads = _threads.Values.AsQueryable().OrderBy(t => t.CreatedTimestamp);

            if (queryOptions is not null)
            {
                threads = queryOptions.ApplyTo(threads) as IQueryable<Thread>;
            }

            return Task.FromResult(threads.AsEnumerable());
        }

        public Task<IEnumerable<Thread>> GetThreadsBySourceAsync(ODataQueryOptions? queryOptions = null, ThreadSource? source = null)
        {
            IQueryable<Thread> threads = _threads.Values.AsQueryable().OrderBy(t => t.CreatedTimestamp);

            if (source != null)
            {
                threads.Where(t => t.Source == source);
            }

            if (queryOptions is not null)
            {
                threads = queryOptions.ApplyTo(threads) as IQueryable<Thread>;
            }

            return Task.FromResult(threads.AsEnumerable());
        }


        public Task<Thread> CreateThreadAsync(Thread thread)
        {
            // Ensure IDs are set
            if (thread.Id == Guid.Empty)
                thread = thread with { Id = Guid.NewGuid() };

            if (thread.StartMessage.Id == Guid.Empty)
                thread = thread with { StartMessage = thread.StartMessage with { Id = Guid.NewGuid() } };

            _threads[thread.Id] = thread;

            // Also store the start message
            _messages[(thread.Id, thread.StartMessage.Id)] = thread.StartMessage;

            return Task.FromResult(thread);
        }

        public Task<bool> DeleteThreadAsync(Guid threadId)
        {
            if (!_threads.ContainsKey(threadId))
            {
                return Task.FromResult(false);
            }

            // Remove all messages for this thread
            var messagesToRemove = _messages.Keys
                .Where(key => key.ThreadId == threadId)
                .ToList();

            foreach (var key in messagesToRemove)
            {
                _messages.Remove(key);
            }

            // Remove all actions for this thread
            var actionsToRemove = _actions.Keys
                .Where(key => key.ThreadId == threadId)
                .ToList();

            foreach (var key in actionsToRemove)
            {
                _actions.Remove(key);
            }

            // Remove all teams mappings for this thread
            string threadIdStr = threadId.ToString();
            var mappingsToRemove = _threadTeamsMappings.Keys
                .Where(key => key.Contains(threadIdStr))
                .ToList();

            foreach (var key in mappingsToRemove)
            {
                _threadTeamsMappings.Remove(key);
            }

            // Remove the thread itself
            _threads.Remove(threadId);

            return Task.FromResult(true);
        }

        public Task<Thread> UpdateThreadTitleAsync(Guid threadId, string newTitle)
        {
            if (!_threads.TryGetValue(threadId, out var thread))
            {
                _logger.LogWarning("Cannot update title: Thread {ThreadId} not found", threadId);
                return Task.FromResult<Thread>(null);
            }

            // Update the title and modified timestamp
            var updatedThread = thread with
            {
                Title = newTitle,
                ModifiedTimestamp = DateTime.UtcNow
            };

            _threads[threadId] = updatedThread;

            _logger.LogInformation("Successfully updated title for thread {ThreadId}", threadId);
            return Task.FromResult(updatedThread);
        }

        #endregion

        #region Message Operations

        public Task<Message> GetMessageAsync(Guid threadId, Guid messageId)
        {
            _messages.TryGetValue((threadId, messageId), out var message);
            return Task.FromResult(message);
        }

        public Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId, ODataQueryOptions? queryOptions)
        {
            var messages = _messages
                .Where(kvp => kvp.Key.ThreadId == threadId)
                .Select(kvp => kvp.Value)
                .OrderBy(m => m.TimeStamp)
                .AsQueryable();

            if (queryOptions is not null)
            {
                messages = queryOptions.ApplyTo(messages) as IQueryable<Message>;
            }

            return Task.FromResult(messages.AsEnumerable());
        }

        public Task<Message> AddMessageAsync(Guid threadId, Message message)
        {
            // Ensure ID is set
            if (message.Id == Guid.Empty)
                message = message with { Id = Guid.NewGuid() };

            if (message.Posted == null)
                message = message with { Posted = new Posted(false) };

            _messages[(threadId, message.Id)] = message;

            // Update the thread's modified timestamp
            if (_threads.TryGetValue(threadId, out var thread))
            {
                _threads[threadId] = thread with { ModifiedTimestamp = DateTime.UtcNow };
            }

            return Task.FromResult(message);
        }

        public Task<bool> DeleteMessageAsync(Guid threadId, Guid messageId)
        {
            // Check if this is a start message
            if (_threads.TryGetValue(threadId, out var thread) &&
                thread.StartMessage.Id == messageId)
            {
                // Can't delete start message without deleting thread
                return Task.FromResult(false);
            }

            return Task.FromResult(_messages.Remove((threadId, messageId)));
        }

        #endregion

        #region Message Feedback Operations

        public Task<MessageFeedback> GetMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId)
        {
            _messageFeedbacks.TryGetValue((threadId, messageFeedbackId), out var messageFeedback);
            return Task.FromResult(messageFeedback);
        }

        public Task<IEnumerable<MessageFeedback>> GetMessageFeedbacksAsync(Guid threadId, ODataQueryOptions? queryOptions)
        {
            var messageFeedbacks = _messageFeedbacks
                .Where(kvp => kvp.Key.ThreadId == threadId)
                .Select(kvp => kvp.Value)
                .OrderBy(m => m.TimeStamp)
                .AsQueryable();

            if (queryOptions is not null)
            {
                messageFeedbacks = queryOptions.ApplyTo(messageFeedbacks) as IQueryable<MessageFeedback>;
            }

            return Task.FromResult(messageFeedbacks.AsEnumerable());
        }

        public Task<MessageFeedback> GetMessageFeedbackNeedingRCAAsync()
        {
            var messageFeedback = _messageFeedbacks
               .Where(kvp => kvp.Value.RootCause == null)
               .Select(kvp => kvp.Value)
               .FirstOrDefault();

            return Task.FromResult(messageFeedback);
        }

        public Task<MessageFeedback> AddOrUpdateMessageFeedbackAsync(Guid threadId, MessageFeedback messageFeedback)
        {
            // Ensure ID is set
            if (messageFeedback.Id == Guid.Empty)
                messageFeedback = messageFeedback with { Id = Guid.NewGuid() };

            _messageFeedbacks[(threadId, messageFeedback.Id)] = messageFeedback;

            return Task.FromResult(messageFeedback);
        }

        public Task<bool> DeleteMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId)
        {
            return Task.FromResult(_messageFeedbacks.Remove((threadId, messageFeedbackId)));
        }

        #endregion

        #region ThreadContext Operations

        public Task<ThreadContext> GetThreadContextAsync(Guid threadId)
        {
            _threadContexts.TryGetValue(threadId, out var action);
            return Task.FromResult(action);
        }

        public Task<IEnumerable<ThreadContext>> GetThreadContextsAsync(ODataQueryOptions? queryOptions)
        {
            var threadContexts = _threadContexts.Values.AsQueryable().OrderBy(tc => tc.ThreadId);

            if (queryOptions is not null)
            {
                threadContexts = queryOptions.ApplyTo(threadContexts) as IOrderedQueryable<ThreadContext>;
            }

            return Task.FromResult(threadContexts.AsEnumerable());
        }

        public Task<ThreadContext> AddThreadContextAsync(ThreadContext context)
        {
            // Ensure ID is set
            if (context.ThreadId == Guid.Empty)
                context = new ThreadContext(Guid.NewGuid(), context.AgentTypeEnum);

            _threadContexts[context.ThreadId] = context;

            return Task.FromResult(context);
        }

        public Task<ThreadContext> UpdateThreadContextAsync(ThreadContext context)
        {
            // Ensure ID is set
            if (context.ThreadId == Guid.Empty)
                context = new ThreadContext(Guid.NewGuid(), context.AgentTypeEnum);

            _threadContexts[context.ThreadId] = context;

            return Task.FromResult(context);
        }

        public Task<bool> DeleteThreadContextAsync(Guid threadId)
        {
            _threadContexts.Remove(threadId);
            return Task.FromResult(true);
        }

        #endregion

        #region Action Operations

        public Task<IEnumerable<Action>> GetActionsAsync(Guid threadId, ODataQueryOptions? queryOptions)
        {
            var actions = _actions
                .Where(kvp => kvp.Key.ThreadId == threadId)
                .Select(kvp => kvp.Value)
                .OrderByDescending(a => a.TimeStamp)
                .AsQueryable();

            if (queryOptions is not null)
            {
                actions = queryOptions.ApplyTo(actions) as IQueryable<Action>;
            }

            return Task.FromResult(actions.AsEnumerable());
        }

        public Task<Action> AddActionAsync(Guid threadId, Action action)
        {
            // Ensure ID is set
            if (action.Id == Guid.Empty)
                action = action with { Id = Guid.NewGuid() };

            _actions[(threadId, action.Id)] = action;

            return Task.FromResult(action);
        }

        public Task<Action> GetActionAsync(Guid threadId, Guid actionId)
        {
            try
            {
                _actions.TryGetValue((threadId, actionId), out var action);
                return Task.FromResult(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving action {ActionId} for thread {ThreadId}", actionId, threadId);
                throw;
            }
        }
        #endregion

        #region AgentContext Operations
        public Task<AgentContext> GetAgentContextAsync(Guid agentContextId, Guid threadId)
        {
            _agentContexts.TryGetValue((threadId, agentContextId), out var agentContext);
            return Task.FromResult(agentContext);
        }

        public Task<IEnumerable<AgentContext>> GetAgentContextsForThreadAsync(Guid threadId)
        {
            return Task.FromResult(_agentContexts
                .Where(kvp => kvp.Key.ThreadId == threadId)
                .Select(kvp => kvp.Value)
                .AsEnumerable());
        }

        public Task<IEnumerable<AgentContext>> GetAllAgentContextsAsync()
        {
            return Task.FromResult(_agentContexts
                .Select(kvp => kvp.Value)
                .AsEnumerable());
        }

        public Task<AgentContext> CreateAgentContextAsync(AgentContext agentContext)
        {
            _agentContexts[(agentContext.ThreadId, agentContext.Id)] = agentContext;
            return Task.FromResult(agentContext);
        }

        public Task<AgentContext> UpdateAgentContextAsync(AgentContext agentContext)
        {
            _agentContexts[(agentContext.ThreadId, agentContext.Id)] = agentContext;
            return Task.FromResult(agentContext);
        }

        public Task<bool> UpdateAgentContextAssignmentInfoAsync(
            Guid agentContextId,
            Guid threadId,
            string? assignedInstanceId,
            DateTimeOffset? expiration)
        {
            _agentContexts.TryGetValue((threadId, agentContextId), out var agentContext);

            if (agentContext == null)
            {
                Task.FromResult(false);
            }

            AgentContext updated = new(
                agentContext.Id,
                agentContext.ThreadId,
                agentContext.AgentType,
                agentContext.ContextState,
                agentContext.WaitInformation,
                agentContext.ApprovalInformation,
                assignedInstanceId,
                expiration);

            _agentContexts[(threadId, agentContextId)] = updated;

            return Task.FromResult(true);
        }

        public Task<bool> DeleteAgentContextAsync(Guid agentContextId, Guid threadId)
        {
            if (!_agentContexts.TryGetValue((threadId, agentContextId), out var agentContext))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(_agentContexts.Remove((threadId, agentContextId)));
        }
        #endregion

        #region ReasoningMessage Operations
        public Task<ReasoningMessage> GetReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId)
        {
            _reasoningMessages.TryGetValue((agentContextId, reasoningMessageId), out var reasoningMessage);
            return Task.FromResult(reasoningMessage);
        }

        public Task<ReasoningMessage> CreateReasoningMessageAsync(ReasoningMessage reasoningMessage)
        {
            _reasoningMessages[(reasoningMessage.AgentContextId, reasoningMessage.Id)] = reasoningMessage;
            return Task.FromResult(reasoningMessage);
        }

        public Task<bool> DeleteReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId)
        {
            if (!_reasoningMessages.TryGetValue((agentContextId, reasoningMessageId), out var reasoningMessage))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(_reasoningMessages.Remove((agentContextId, reasoningMessageId)));
        }
        #endregion

        #region AgentContext Operations
        public Task<AgentChatHistory> GetAgentChatHistoryAsync(Guid agentContextId)
        {
            _agentChatHistories.TryGetValue(agentContextId, out var agentChatHistory);
            return Task.FromResult(agentChatHistory);
        }

        public Task<AgentChatHistory> CreateAgentChatHistoryAsync(AgentChatHistory agentChatHistory)
        {
            _agentChatHistories[agentChatHistory.AgentContextId] = agentChatHistory;
            return Task.FromResult(agentChatHistory);
        }

        public Task<AgentChatHistory> UpdateAgentChatHistoryAsync(AgentChatHistory agentChatHistory)
        {
            _agentChatHistories[agentChatHistory.AgentContextId] = agentChatHistory;
            return Task.FromResult(agentChatHistory);
        }

        public Task<AgentChatHistory> AddReasoningMessagesToChatHistoryAsync(AgentChatHistory agentChatHistory, params IEnumerable<ReasoningMessage> reasoningMessages)
        {
            foreach (var reasoningMessage in reasoningMessages)
            {
                agentChatHistory.ReasoningMessageIds.Add(reasoningMessage.Id);
                _reasoningMessages[(agentChatHistory.AgentContextId, reasoningMessage.Id)] = reasoningMessage;
            }

            _agentChatHistories[agentChatHistory.AgentContextId] = agentChatHistory;
            return Task.FromResult(agentChatHistory);
        }

        public Task<bool> DeleteAgentChatHistoryAsync(Guid agentContextId)
        {
            if (!_agentChatHistories.TryGetValue(agentContextId, out var agentContext))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(_agentChatHistories.Remove(agentContextId));
        }
        #endregion

        #region ApprovalV2 Operations
        public Task<ApprovalV2> GetApprovalV2Async(Guid approvalIdV2, Guid agentContextId)
        {
            _approvalv2s.TryGetValue((agentContextId, approvalIdV2), out var approvalV2);
            return Task.FromResult(approvalV2);
        }

        public Task<IEnumerable<ApprovalV2>> GetAllApprovalV2sAsync()
        {
            return Task.FromResult(_approvalv2s.Values.AsEnumerable());
        }

        public Task<ApprovalV2> CreateApprovalV2Async(ApprovalV2 approvalV2)
        {
            _approvalv2s[(approvalV2.AgentContextId, approvalV2.Id)] = approvalV2;
            return Task.FromResult(approvalV2);
        }

        public Task<ApprovalV2> UpdateApprovalV2Async(ApprovalV2 approvalV2)
        {
            _approvalv2s[(approvalV2.AgentContextId, approvalV2.Id)] = approvalV2;
            return Task.FromResult(approvalV2);
        }

        public Task<Approval> CreateApprovalAsync(Approval approval)
        {
            _approvals[(Guid.Parse(approval.ThreadId), approval.Id)] = approval;
            return Task.FromResult(approval);
        }

        public Task<Approval> GetApprovalAsync(Guid threadId, Guid approvalId)
        {
            return Task.FromResult(_approvals.TryGetValue((threadId, approvalId), out var approval) ? approval : null);
        }

        public Task<Approval> GetApprovalAsync(Guid threadId, string title)
        {
            var approval = _approvals.Values.FirstOrDefault(a => a.ThreadId == threadId.ToString() && a.Title == title, null);
            return Task.FromResult(approval);
        }

        public Task<Approval> UpdateApprovalAsync(Approval approval)
        {
            _approvals[(Guid.Parse(approval.ThreadId), approval.Id)] = approval;
            return Task.FromResult(approval);
        }

        public Task<IList<Approval>> GetApprovalsAsync(Guid threadId)
        {
            var approvals = _approvals
                .Where(kvp => kvp.Key.ThreadId == threadId)
                .Select(kvp => kvp.Value)
                .ToList();
            return Task.FromResult((IList<Approval>)approvals);
        }

        public Task<Action> GetLatestToolCallAction(Guid threadId, string toolName)
        {
            var action = _actions
                .Where(kvp => kvp.Key.ThreadId == threadId && kvp.Value.ToolName == toolName)
                .Select(kvp => kvp.Value)
                .OrderByDescending(a => a.TimeStamp)
                .FirstOrDefault();
            return Task.FromResult(action);
        }
        #endregion
    }
}

