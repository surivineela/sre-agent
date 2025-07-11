// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.AspNetCore.OData.Query;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Core.Interfaces;

public interface IThreadRepository
{
    Task<Thread> GetThreadAsync(Guid threadId);
    Task<IEnumerable<Thread>> GetThreadsAsync(ODataQueryOptions? queryOptions = null, ActionSeverity? severity = null , ThreadType? threadType = ThreadType.Prod);
    Task<IEnumerable<Thread>> GetThreadsBySourceAsync(ODataQueryOptions? queryOptions = null, ThreadSource? source = null, IncidentType? incidentType = null, DateTime? createdAfter = null);
    Task<Thread> CreateThreadAsync(Thread thread);
    Task<bool> DeleteThreadAsync(Guid threadId);

    Task<Thread> UpdateThreadTitleAsync(Guid threadId, string newTitle);
    Task<Thread> UpdateThreadReadMarkAsync(Guid threadId, DateTime lastReadTime);
    Task<Thread> UpdateThreadEvaluatedTimestampAsync(Guid threadId, DateTime evaluatedTimestamp);
    Task<Thread> UpdateThreadAgentModeAsync(Guid threadId, string? agentMode);

    Task<Message> GetMessageAsync(Guid threadId, Guid messageId);
    Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId, ODataQueryOptions? queryOptions = null);
    Task<int> GetUnreadMessagesCountAsync(Guid threadId, DateTime? lastReadTime);
    Task<Message> AddMessageAsync(Guid threadId, Message message);
    Task<Message> UpdateMessageAsync(Guid threadId, Message message);
    Task<bool> DeleteMessageAsync(Guid threadId, Guid messageId);

    Task<ThreadContext> GetThreadContextAsync(Guid threadId);
    Task<IEnumerable<ThreadContext>> GetThreadContextsAsync(ODataQueryOptions? queryOptions = null);
    Task<ThreadContext> AddThreadContextAsync(ThreadContext context);
    Task<ThreadContext> UpdateThreadContextAsync(ThreadContext context);
    Task<bool> DeleteThreadContextAsync(Guid threadId);

    Task<IEnumerable<Action>> GetActionsAsync(Guid threadId, ODataQueryOptions? queryOptions = null);
    Task<Action> GetActionAsync(Guid threadId, Guid actionId);
    Task<Action> AddOrUpdateActionAsync(Guid threadId, Action action);
    Task<IEnumerable<string>> GetThreadIdsWithActionSeverityAsync(ActionSeverity? severity);
    Task<IEnumerable<Action>> GetAllActionsAsync();

    Task<MessageFeedback> GetMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId);
    Task<IEnumerable<MessageFeedback>> GetMessageFeedbacksAsync(Guid threadId, ODataQueryOptions? queryOptions = null);
    Task<MessageFeedback> AddOrUpdateMessageFeedbackAsync(Guid threadId, MessageFeedback messageFeedback);
    Task<bool> DeleteMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId);
    Task<MessageFeedback> GetMessageFeedbackNeedingRCAAsync();

    Task<AgentContext?> GetAgentContextAsync(Guid agentContextId, Guid threadId);
    Task<IEnumerable<AgentContext>> GetAgentContextsForThreadAsync(Guid threadId);
    Task<IEnumerable<AgentContext>> GetAllAgentContextsAsync();
    Task<AgentContext> CreateAgentContextAsync(AgentContext agentContext);
    Task<AgentContext> UpdateAgentContextAsync(AgentContext agentContext);
    Task<bool> UpdateAgentContextAssignmentInfoAsync(
        Guid agentContextId,
        Guid threadId,
        string? assignedInstanceId,
        DateTimeOffset? expiration);
    Task<bool> DeleteAgentContextAsync(Guid agentContextId, Guid threadId);

    Task<ReasoningMessage> GetReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId);
    Task<ReasoningMessage> CreateReasoningMessageAsync(ReasoningMessage reasoningMessage);
    Task<bool> DeleteReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId);

    Task<AgentChatHistory> GetAgentChatHistoryAsync(Guid agentContextId);
    Task<AgentChatHistory> CreateAgentChatHistoryAsync(AgentChatHistory agentChatHistory);
    Task<AgentChatHistory> UpdateAgentChatHistoryAsync(AgentChatHistory agentChatHistory);
    Task<AgentChatHistory> AddReasoningMessagesToChatHistoryAsync(AgentChatHistory agentChatHistory, params IEnumerable<ReasoningMessage> reasoningMessages);
    Task<bool> DeleteAgentChatHistoryAsync(Guid agentContextId);

    Task<Approval> CreateApprovalAsync(Approval approval);
    Task<IList<Approval>> GetApprovalsAsync(Guid threadId);
    Task<Approval> GetApprovalAsync(Guid threadId, Guid approvalId);
    Task<Approval> GetApprovalAsync(Guid threadId, string title);
    Task<Approval> UpdateApprovalAsync(Approval approval);

    Task<ApprovalV2> GetApprovalV2Async(Guid approvalIdV2, Guid agentContextId);
    Task<IEnumerable<ApprovalV2>> GetAllApprovalV2sAsync();
    Task<ApprovalV2> CreateApprovalV2Async(ApprovalV2 approvalV2);
    Task<ApprovalV2> UpdateApprovalV2Async(ApprovalV2 approvalV2);

    Task<GitHubAccessToken> GetGitHubAccessTokenAsync();
    Task<GitHubAccessToken> CreateOrUpdateGitHubAccessTokenAsync(GitHubAccessToken gitHubAccessToken);
    Task<bool> DeleteGitHubAccessTokenAsync();
    Task<AzureDevOpsAccessToken> GetAzureDevOpsAccessTokenAsync(string resourceId);
    Task<AzureDevOpsAccessToken> CreateOrUpdateAzureDevOpsAccessTokenAsync(AzureDevOpsAccessToken azureDevOpsAccessToken, string resourceId);
    Task<bool> DeleteAzureDevOpsAccessTokenAsync(string resourceId);
    Task<AzCliExecution> ListPendingAzCliExecutionAsync(Guid threadId);
    Task<AzCliExecution> GetAzCliExecutionAsync(Guid threadId, Guid executionId);
    Task<AzCliExecution> CreateAzCliExecutionAsync(Guid threadId, AzCliExecution execution);
    Task<AzCliExecution> UpdateAzCliExecutionAsync(Guid threadId, AzCliExecution execution);
    Task<AzCliExecution> UpdateAzCliExecutionOutputAsync(Guid threadId, Guid executionId, string output, string? error = null);
    Task<KubectlExecution> ListPendingKubectlExecutionAsync(Guid threadId);
    Task<KubectlExecution> GetKubectlExecutionAsync(Guid threadId, Guid executionId);
    Task<KubectlExecution> CreateKubectlExecutionAsync(Guid threadId, KubectlExecution execution);
    Task<KubectlExecution> UpdateKubectlExecutionAsync(Guid threadId, KubectlExecution execution);
    Task<KubectlExecution> UpdateKubectlExecutionOutputAsync(Guid threadId, Guid executionId, string output, string? error = null);
}
