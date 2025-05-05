// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.Security;

namespace Agent.Runtime.Communication;

public class InboundCommunicationService : IAgentInboundCommunicationService
{
    private readonly IAgent _metaAgent;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly IThreadRepository _repository;
    private readonly ILogger<InboundCommunicationService> _logger;
    private readonly SinkService _sinkService;
    private readonly ThreadService _threadService;
    private readonly IChatClient _chatClient;
    private readonly IGraphDBPlugin _graphDbPlugin;
    private readonly IGithubIssuePlugin _githubIssuePlugin;

    private readonly IPostToTeamsPlugin _teamsPlugin;

    public InboundCommunicationService(
        IAgent metaAgent,
        DurableTaskClient durableTaskClient,
        IThreadOrchestrationManager mappingManager,
        IThreadRepository repository,
        SinkService sinkService,
        ThreadService threadService,
        IPostToTeamsPlugin teamsPlugin,
        ILogger<InboundCommunicationService> logger,
        IChatClient chatClient,
        IGraphDBPlugin graphDBPlugin,
        IGithubIssuePlugin githubIssuePlugin)
    {
        _metaAgent = metaAgent;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
        _repository = repository;
        _sinkService = sinkService;
        _threadService = threadService;
        _teamsPlugin = teamsPlugin;
        _logger = logger;
        _chatClient = chatClient;
        _graphDbPlugin = graphDBPlugin;
        _githubIssuePlugin = githubIssuePlugin;
    }

    public async Task<(Core.Models.Api.v1.Thread, AgentContext)> CreateAgentThread(
        string title,
        string message,
        AgentTypeEnum agentTypeEnum,
        ThreadSource source = ThreadSource.Agent,
        string incidentId = "",
        IncidentSource? incidentSource = null,
        bool isDailyReport = false)
    {
        return await CreateThread(title, message, source, agentTypeEnum, incidentId: incidentId, incidentSource: incidentSource, isDailyReport: isDailyReport);
    }

    public async Task<Core.Models.Api.v1.Thread> CreateAlertThreadWithTeams(
        string title,
        string message,
        AgentTypeEnum agentTypeEnum,
        ThreadSource source = ThreadSource.Alert)
    {
        var outboundConfig = new OutboundConfiguration { Teams = new Teams { Enabled = true } };
        (var thread, var agentContext) = await CreateThread(title, message, source, agentTypeEnum, outboundConfig);
        await _teamsPlugin.CreateTeamsThread(thread.Id.ToString(), thread.StartMessage.Text, thread.StartMessage.Id.ToString());

        return thread;
    }

    public async Task ProcessAlertMessageAsync(ThreadMessage message)
    {
        // TODO(jianbosun) - this is a placeholder for the alert message processing
        // In the future, we may want to add some logic here to handle alert messages differently
        await ProcessUserMessageAsync(message);
    }

    public async Task<Guid> AppendAgentImageMessage(Guid threadId, string message)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        return await _sinkService.SinkAgentMessageAsync(threadId, message, isImageContent: true);
    }

    public async Task<InboundServiceResponse> ProcessUserMessageAsync(ThreadMessage threadMessage)
    {
        try
        {
            string orchestrationInstanceId = "";
            Guid responseMessageId = Guid.Empty;

            // Check if an orchestration already exists for this thread
            AgentContext agentContext = await _repository.GetAgentContextAsync(agentContextId: threadMessage.AgentContextId, threadId: threadMessage.ThreadId);
            AgentChatHistory agentChatHistory = await _repository.GetAgentChatHistoryAsync(threadMessage.AgentContextId);

            orchestrationInstanceId = agentContext != null ? await _threadService.GetOrchestrationInstanceId(agentContext.ThreadId) : orchestrationInstanceId;

            // we don't need to sink user message if the message is the start message
            var thread = await _repository.GetThreadAsync(threadMessage.ThreadId);
            ReasoningMessage? reasoningMessage = null;
            if (threadMessage?.MessageId != thread?.StartMessage?.Id)
            {
                await _sinkService.SinkUserMessageAsync(threadMessage);
                reasoningMessage = new ReasoningMessage(
                    Id: Guid.NewGuid(),
                    AgentContextId: threadMessage.AgentContextId,
                    Role: ReasoningMessageRoleEnum.User,
                    SerializedChatMessage: JsonSerializer.Serialize(new ChatMessage(ChatRole.User, threadMessage.Message)));
                await _repository.CreateReasoningMessageAsync(reasoningMessage);

                await _repository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage);
            }

            if (!string.IsNullOrEmpty(orchestrationInstanceId))
            {

                var existingOrchestration = await _durableTaskClient.GetInstanceAsync(orchestrationInstanceId,
                    getInputsAndOutputs: true, CancellationToken.None);
                // Check for failed orchestrations and clean them if needed
                bool cleaned = await _threadService.CleanOrchestration(
                    thread.Id,
                    orchestrationInstanceId,
                    existingOrchestration);

                // If the orchestration was cleaned, get the updated orchestration ID (might be empty now)
                if (cleaned)
                {
                    orchestrationInstanceId = await _threadService.GetOrchestrationInstanceId(agentContext.ThreadId);
                }
            }

            if (string.IsNullOrEmpty(orchestrationInstanceId))
            {
                // No existing orchestration, create a new one
                _logger.LogInternalInformation("No existing orchestration for thread: {ThreadId}", threadMessage.ThreadId);

                string agentResponse = string.Empty;
                bool isComplete = false;

                if (agentContext != null && AgentTypeHelper.IsScannerAgent(agentContext.AgentType))
                {
                    ScannerSubAgent scannerSubAgent = null;
                    switch (agentContext.AgentType)
                    {
                        case AgentTypeEnum.CVE:
                            scannerSubAgent = new CVEAgent(_chatClient, _graphDbPlugin, _githubIssuePlugin, _sinkService, _repository);
                            break;
                        case AgentTypeEnum.SourceCode:
                            scannerSubAgent = new SourceCodeAgent(_chatClient, _graphDbPlugin, _sinkService, _repository);
                            break;
                        default:
                            throw new NotSupportedException($"Scanner agent type {agentContext.AgentType} is not supported.");
                    }

                    if (scannerSubAgent != null)
                    {
                        agentResponse = await scannerSubAgent.DoWork(agentContext: agentContext, agentChatHistory: agentChatHistory, threadMessage.Message);
                    }
                }
                else if (agentContext != null && agentContext.HandoffToAgentContextId != null && reasoningMessage != null)
                {
                    // this context handed off to another context, need to add a reasoning message to that one as well, then the background processor will handle it

                    var handoffToContext = await _repository.GetAgentContextAsync(agentContextId: agentContext.HandoffToAgentContextId.Value, threadId: threadMessage.ThreadId)
                        ?? throw new InvalidOperationException($"Handoff to agent context {agentContext.HandoffToAgentContextId} not found for thread {threadMessage.ThreadId}.");

                    var handoffToChatHistory = await _repository.GetAgentChatHistoryAsync(handoffToContext.Id);

                    var handoffReasoningMessage = reasoningMessage with
                    {
                        Id = Guid.NewGuid(),
                        AgentContextId = handoffToContext.Id
                    };

                    await _repository.CreateReasoningMessageAsync(handoffReasoningMessage);
                    await _repository.AddReasoningMessagesToChatHistoryAsync(handoffToChatHistory, handoffReasoningMessage);

                    return new InboundServiceResponse(threadMessage.ThreadId, responseMessageId, orchestrationInstanceId);
                }
                else
                {
                    // Process the message with MetaAgent
                    agentResponse = await _metaAgent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);
                }

                responseMessageId = await _sinkService.SinkAgentMessageAsync(agentContext.ThreadId, agentResponse);
            }
            else
            {
                // TODO (jianbosun):
                // For now, we assume there's only 1:1 mapping for threadId and orchestrationInstanceId,
                // but we may change this to allow multiple orchestrations per thread, e.g. to choose sub-agent type in one thread as a different orchestration.
                // This will enable us for scenarios that need to share chat history with multiple orchestrations for different purposes.

                // Existing orchestration, raise an event to it
                _logger.LogInternalInformation("Sending message to existing orchestration for thread: {ThreadId}", threadMessage.ThreadId);
                await _durableTaskClient.RaiseEventAsync(
                    orchestrationInstanceId,
                    "NewChatMessage",
                    new ChatMessage(ChatRole.User, threadMessage.Message));
            }

            return new InboundServiceResponse(threadMessage.ThreadId, responseMessageId, orchestrationInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error processing user message for thread: {ThreadId}", threadMessage.ThreadId);
            throw;
        }
    }

    public async Task<MessageFeedback> ProcessFeedbackAsync(ThreadMessageFeedback threadMessageFeedback)
    {
        try
        {
            // Check if an orchestration already exists for this thread
            var messages = await _repository.GetMessagesAsync(threadMessageFeedback.ThreadId);

            var messageFeedback = new MessageFeedback(
                Id: threadMessageFeedback.MessageFeedbackId,
                ThreadId: threadMessageFeedback.ThreadId,
                TimeStamp: DateTime.UtcNow,
                Messages: messages.ToList(),
                IsPositiveFeedback: threadMessageFeedback.IsPositive,
                FeedbackText: threadMessageFeedback.FeedbackText,
                RootCause: null);

            await _repository.AddOrUpdateMessageFeedbackAsync(threadMessageFeedback.ThreadId, messageFeedback);

            return messageFeedback;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error processing feedback for thread: {ThreadId}", threadMessageFeedback.ThreadId);
            throw;
        }
    }

    private async Task<(Core.Models.Api.v1.Thread, Core.Models.Api.v1.AgentContext)> CreateThread(
        string title,
        string message,
        ThreadSource source,
        AgentTypeEnum agentTypeEnum,
        OutboundConfiguration? outboundConfiguration = null,
        string incidentId = "",
        IncidentSource? incidentSource = null,
        bool isDailyReport = false)
    {
        var now = DateTime.UtcNow;
        var startMessage = new Message(
            Guid.NewGuid(),
            now,
            new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            message,
            false,
            new Posted(false),
            IsDailyReport : isDailyReport
        );

        var thread = new Core.Models.Api.v1.Thread(
            Id: Guid.NewGuid(),
            Title: title,
            StartMessage: startMessage,
            LastMessage: startMessage,
            CreatedTimestamp: now,
            ModifiedTimestamp: now,
            Source: source,
            IncidentSource: incidentSource
        );

        if (incidentId != string.Empty)
        {
            thread.Status = new Status
            {
                IncidentStatus = new Core.Models.Api.v1.IncidentStatus
                {
                    IncidentId = incidentId,
                }
            };
        }

        var agentContext = new AgentContext(
            Id: Guid.NewGuid(),
            ThreadId: thread.Id,
            AgentType: agentTypeEnum,
            ContextState: ContextStateEnum.Idle,
            WaitInformation: null,
            ApprovalInformation: null
        );

        var startReasoningMessage = new ReasoningMessage(
            Id: Guid.NewGuid(),
            AgentContextId: agentContext.Id,
            Role: ReasoningMessageRoleEnum.Assistant,
            SerializedChatMessage: JsonSerializer.Serialize(new ChatMessage(ChatRole.Assistant, message)));

        var agentChatHistory = new AgentChatHistory(AgentContextId: agentContext.Id, ReasoningMessageIds: new List<Guid> { startReasoningMessage.Id });

        // TODO - how should we share implementation with process user message and make sure fan out occurs?
        await _repository.CreateThreadAsync(thread);
        await _repository.CreateAgentContextAsync(agentContext);
        await _repository.CreateAgentChatHistoryAsync(agentChatHistory);

        await _repository.AddMessageAsync(thread.Id, thread.StartMessage);
        await _repository.CreateReasoningMessageAsync(startReasoningMessage);

        return (thread, agentContext);
    }

    public async Task<Core.Models.Api.v1.Thread> CreateAndProcessIncidentThread(string title, string message, IncidentSource incidentSource, List<IncidentDiscussion> discussions)
    {
        (var thread, var agentContext) = await CreateAgentThread(
            title: title,
            message: message,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.Incident,
            incidentSource: incidentSource
        );

        await AddNewDiscussionsToIncidentThread(thread.Id, discussions);

        var agentMessage = $"**Detected the incident**. I'm starting to investigate and see how I can help.";
        await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

        await ProcessAlertMessageAsync(new ThreadMessage(
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage.Id,
            Message: "",
            UserId: "incident-system",
            DisplayName: incidentSource.IncidentType.ToString(),
            Timestamp: DateTime.UtcNow
        ));

        return thread;
    }

    public async Task AddNewDiscussionsToIncidentThread(Guid incidentThreadId, List<IncidentDiscussion> discussions)
    {
        foreach (var discussion in discussions)
        {
            var discussionMessage = new Message(
                Id: Guid.NewGuid(),
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.SREAgent, discussion.UserId, discussion.UserDisplayName),
                Text: $"{discussion.UserDisplayName} commented at {discussion.CreatedTimestamp}: {discussion.Message}",
                IncidentDiscussionId: discussion.Id);
            await _repository.AddMessageAsync(incidentThreadId, discussionMessage);
        }
    }
}
