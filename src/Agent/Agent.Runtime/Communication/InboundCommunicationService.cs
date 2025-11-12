// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Runtime.IncidentHandlerAgent;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class InboundCommunicationService : IAgentInboundCommunicationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgent _metaAgent;
    private readonly IIncidentHandlerAgent _incidentHandlerAgent;
    private readonly IThreadRepository _repository;
    private readonly ILogger<InboundCommunicationService> _logger;
    private readonly CustomerLogger _customerLogger;
    private readonly SinkService _sinkService;
    private readonly ThreadService _threadService;
    private readonly IPostToTeamsPlugin _teamsPlugin;
    private readonly IReasoningLoopManager _reasoningLoopManager;
    private readonly ActionSettings _actionSettings;
    private readonly bool _useAgentFramework;

    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

    public InboundCommunicationService(
        IAgent metaAgent,
        IIncidentHandlerAgent incidentHandlerAgent,
        IThreadRepository repository,
        SinkService sinkService,
        ThreadService threadService,
        IPostToTeamsPlugin teamsPlugin,
        ILogger<InboundCommunicationService> logger,
        CustomerLogger customerLogger,
        IServiceProvider serviceProvider,
        IReasoningLoopManager reasoningLoopManager,
        CoreSettings coreSettings,
        ActionSettings actionSettings,
        IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _metaAgent = metaAgent;
        _incidentHandlerAgent = incidentHandlerAgent;
        _repository = repository;
        _sinkService = sinkService;
        _threadService = threadService;
        _teamsPlugin = teamsPlugin;
        _logger = logger;
        _customerLogger = customerLogger;
        _serviceProvider = serviceProvider;
        _reasoningLoopManager = reasoningLoopManager;
        _useAgentFramework = coreSettings.UseAgentFramework;
        _actionSettings = actionSettings;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    public async Task<(Core.Models.Api.v1.Thread, AgentContext)> CreateAgentThread(
        string title,
        string message,
        AgentTypeEnum agentTypeEnum,
        ThreadSource source = ThreadSource.Agent,
        string incidentId = "",
        IncidentSource? incidentSource = null,
        bool isDailyReport = false,
        List<string>? AllowedTools = null,
        ThreadType threadMode = ThreadType.Prod,
        string overrideAgentMode = "",
        IncidentDetails? incidentDetails = null
        )
    {
        return await CreateAgentInitiatedThread(title, message, source, agentTypeEnum, incidentId: incidentId, incidentSource: incidentSource, isDailyReport: isDailyReport, AllowedTools: AllowedTools, threadType: threadMode, overrideAgentMode: overrideAgentMode, incidentDetails: incidentDetails);
    }

    public async Task<Core.Models.Api.v1.Thread> CreateAlertThreadWithTeams(
        string title,
        string message,
        AgentTypeEnum agentTypeEnum,
        ThreadSource source = ThreadSource.Alert)
    {
        var outboundConfig = new OutboundConfiguration { Teams = new Teams { Enabled = true } };
        (var thread, var agentContext) = await CreateAgentInitiatedThread(title, message, source, agentTypeEnum, outboundConfig);
        await _teamsPlugin.CreateTeamsThread(thread.Id.ToString(), thread.StartMessage?.Text ?? string.Empty, thread.StartMessage?.Id.ToString() ?? string.Empty);

        return thread;
    }

    public async Task ProcessAlertMessageAsync(ThreadMessage message, bool defaultHandler = true)
    {
        // TODO(jianbosun) - this is a placeholder for the alert message processing
        // In the future, we may want to add some logic here to handle alert messages differently
        await ProcessIncidentMessageAsync(message, defaultHandler);
    }

    public async Task<Guid> AppendAgentImageMessage(Guid threadId, string message)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        return await _sinkService.SinkAgentMessageAsync(threadId, message, isImageContent: true);
    }

    private async Task<InboundServiceResponse> ProcessMessageWithAgentFrameworkAsync(ThreadMessage threadMessage)
    {
        AgentContext? agentContext = await _repository.GetAgentContextAsync(agentContextId: threadMessage.AgentContextId, threadId: threadMessage.ThreadId);

        // we don't need to sink user message if the message is the start message
        var thread = await _repository.GetThreadAsync(threadMessage.ThreadId);
        if (threadMessage.MessageId != thread?.StartMessage?.Id)
        {
            await _sinkService.SinkUserMessageAsync(threadMessage);
        }

        var chatRole = ChatRole.User;
        if (string.Equals(threadMessage.UserId, "agent-default", StringComparison.OrdinalIgnoreCase))
        {
            // If the message is from the SRE agent, we treat it as a system message
            chatRole = ChatRole.System;
        }

        if (!string.IsNullOrEmpty(threadMessage.AgentName))
        {
            await _reasoningLoopManager.SetCurrentAgentAsync(agentContext!, threadMessage.AgentName);
        }

        await _reasoningLoopManager.AppendNewMessageAsync(
            context: agentContext!,
            msg: new ChatMessage(chatRole, threadMessage.Message),
            conversationModifier: threadMessage.ConversationModifier,
            // uncomment this line and comment the one above for testing until the frontend change is in
            //conversationModifier: ConversationModifierEnum.DeepInvestigation,
            cancellationToken: default);

        return new InboundServiceResponse(threadMessage.ThreadId, Guid.Empty, string.Empty);
    }

    public async Task<InboundServiceResponse> ProcessUserMessageAsync(ThreadMessage threadMessage)
    {
        // Start customer trace scope that will span the entire request lifecycle
        using var traceScope = CustomerTraceManager.StartScope();

        // Start customer activity for message processing
        using var customerActivity = _customerLogger.StartMessageProcessing(
            threadMessage.Message,
            threadMessage.ThreadId,
            threadMessage.UserId);

        if (customerActivity != null)
        {
            // Associate the customer activity with the trace scope so it's available throughout the request
            traceScope.SetActivity(customerActivity);

            customerActivity.AddAttribute("display_name", threadMessage.DisplayName);
            customerActivity.RecordEvent("message_received", "User message received for processing");

            // Add request-level metadata to the trace scope
            //traceScope.AddMetadata("request_type", "user_message");
            traceScope.AddMetadata("thread_id", threadMessage.ThreadId.ToString());
            //traceScope.AddMetadata("user_name", threadMessage.DisplayName ?? "unknown");
            traceScope.AddMetadata("gen_ai.provider.name", "Microsoft.SREAgent");
        }
        _customerLogger.LogUserInputEvents("UserMessageInput", threadMessage.Message, isStartOfTrace: true, properties: new Dictionary<string, string>
        {
            { "ChatThreadId", threadMessage.ThreadId.ToString() },
            { "UserDisplayName", threadMessage.DisplayName }
        });
        _customerLogger.LogMessage($"[ChatThreadId {threadMessage.ThreadId}] Processing user message: {threadMessage.Message}",
            properties: new Dictionary<string, string>
        {
            { "UserId", threadMessage.UserId ?? "" },
            { "DisplayName", threadMessage.DisplayName }
        });
        _customerLogger.LogCustomEvent("MetaAgent", new Dictionary<string, string>
        {
            { "ChatThreadId", threadMessage.ThreadId.ToString() },
            { "Message", threadMessage.Message },
            { "UserId", threadMessage.UserId ?? "" },
            { "DisplayName", threadMessage.DisplayName }
        });

        try
        {
            var result = await ProcessMessageWithAgentFrameworkAsync(threadMessage);

            return result;
        }
        catch (Exception ex)
        {
            customerActivity?.SetError("Failed to process user message.", ex.GetType().Name);
            throw;
        }
    }

    public async Task<InboundServiceResponse> ProcessIncidentMessageAsync(ThreadMessage threadMessage, bool defaultHandler = true)
    {
        // Start customer trace scope that will span the entire request lifecycle
        using var traceScope = CustomerTraceManager.StartScope();

        // Start customer activity for incident processing
        using var customerActivity = _customerLogger.StartIncidentHandling(
            threadMessage.ThreadId.ToString(),
            "general_incident");

        if (customerActivity != null)
        {
            // Associate the customer activity with the trace scope so it's available throughout the request
            traceScope.SetActivity(customerActivity);

            customerActivity.AddAttribute("message_length", threadMessage.Message.Length.ToString());
            customerActivity.RecordEvent("incident_received", "Incident message received for processing");

            // Add request-level metadata to the trace scope
            traceScope.AddMetadata("request_type", "incident_message");
            traceScope.AddMetadata("thread_id", threadMessage.ThreadId.ToString());
        }

        _logger.LogInternalInformation($"ProcessIncidentMessageAsync: Started processing incident message: {threadMessage.Message}. ThreadId: {threadMessage.ThreadId}");
        _customerLogger.LogUserInputEvents("IncidentMessageInput", threadMessage.Message, isStartOfTrace: true, properties: new Dictionary<string, string>
        {
            { "ChatThreadId", threadMessage.ThreadId.ToString() },
            { "Message", threadMessage.Message }
        });

        _customerLogger.LogMessage($"[ChatThreadId {threadMessage.ThreadId}] Processing incident message: {threadMessage.Message}");
        _customerLogger.LogCustomEvent("MetaAgent", new Dictionary<string, string>
        {
            { "ChatThreadId", threadMessage.ThreadId.ToString() },
            { "Message", threadMessage.Message }
        });

        // TODO Remove this line
        defaultHandler = true; // route to the path we want.
        if (_useAgentFramework && defaultHandler)
        {
            try
            {
                var result = await ProcessMessageWithAgentFrameworkAsync(threadMessage);
                customerActivity?.SetSuccess("Incident processed via agent framework");
                return result;
            }
            catch (Exception ex)
            {
                customerActivity?.SetError("Failed to process incident via agent framework.", ex.GetType().Name);
                throw;
            }
        }

        try
        {
            string orchestrationInstanceId = "";
            Guid responseMessageId = Guid.Empty;

            // Check if an orchestration already exists for this thread
            AgentContext? agentContext = await _repository.GetAgentContextAsync(agentContextId: threadMessage.AgentContextId, threadId: threadMessage.ThreadId);
            AgentChatHistory? agentChatHistory = await _repository.GetAgentChatHistoryAsync(threadMessage.AgentContextId);

            orchestrationInstanceId = agentContext != null ? await _threadService.GetOrchestrationInstanceId(agentContext.ThreadId) : orchestrationInstanceId;

            // we don't need to sink user message if the message is the start message
            var thread = await _repository.GetThreadAsync(threadMessage.ThreadId);
            ReasoningMessage? reasoningMessage = null;
            if (threadMessage.MessageId != thread?.StartMessage?.Id)
            {
                await _sinkService.SinkUserMessageAsync(threadMessage);
                reasoningMessage = new ReasoningMessage(
                    Id: Guid.NewGuid(),
                    AgentContextId: threadMessage.AgentContextId,
                    Role: ReasoningMessageRoleEnum.User,
                    SerializedChatMessage: JsonSerializer.Serialize(new ChatMessage(ChatRole.User, threadMessage.Message)));
                await _repository.CreateReasoningMessageAsync(reasoningMessage);

                if (agentChatHistory != null)
                {
                    await _repository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage);
                }
            }

            if (string.IsNullOrEmpty(orchestrationInstanceId))
            {
                // No existing orchestration, create a new one
                _logger.LogInternalInformation("ProcessIncidentMessageAsync: No existing orchestration for thread: {ThreadId}", threadMessage.ThreadId);

                var agentResponse = string.Empty;

                if (agentContext != null && agentContext.AgentType == AgentTypeEnum.Incident && agentChatHistory != null)
                {
                    agentResponse = await _incidentHandlerAgent.ProcessIncidentAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);
                }
                else if (agentContext != null && agentChatHistory != null)
                {
                    // Process the message with MetaAgent
                    agentResponse = await _metaAgent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);
                }
                else
                {
                    throw new InvalidOperationException("Agent context is null for thread: " + threadMessage.ThreadId);
                }
                //responseMessageId = await _sinkService.SinkAgentMessageAsync(agentContext.ThreadId, agentResponse);
            }

            return new InboundServiceResponse(threadMessage.ThreadId, responseMessageId, orchestrationInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ProcessIncidentMessageAsync: Error processing incident message for thread: {ThreadId}", threadMessage.ThreadId);
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

            if (threadMessageFeedback.IsPositive)
            {
                _logger.LogAgentAction(
                    action: AgentActionEvents.ThumbsUp,
                    parameter: $"{threadMessageFeedback.ThreadId}",
                    status: AgentActionStatus.Success,
                    duration: 0,
                    threadId: threadMessageFeedback.ThreadId.ToString());
            }
            else
            {
                _logger.LogAgentAction(
                    action: AgentActionEvents.ThumbsDown,
                    parameter: $"{threadMessageFeedback.ThreadId}",
                    status: AgentActionStatus.Success,
                    duration: 0,
                    threadId: threadMessageFeedback.ThreadId.ToString());
            }

            await _repository.AddOrUpdateMessageFeedbackAsync(threadMessageFeedback.ThreadId, messageFeedback);

            return messageFeedback;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error processing feedback for thread: {ThreadId}", threadMessageFeedback.ThreadId);
            throw;
        }
    }

    // CreateAgentInitiatedThread just creates a thread and agent context without triggering reasoning loop.
    private async Task<(Core.Models.Api.v1.Thread, Core.Models.Api.v1.AgentContext)> CreateAgentInitiatedThread(
        string title,
        string message,
        ThreadSource source,
        AgentTypeEnum agentTypeEnum,
        OutboundConfiguration? outboundConfiguration = null,
        string incidentId = "",
        IncidentSource? incidentSource = null,
        bool isDailyReport = false,
        List<string>? AllowedTools = null,
        ThreadType threadType = ThreadType.Prod,
        string overrideAgentMode = "",
        IncidentDetails? incidentDetails = null
        )
    {
        var now = DateTime.UtcNow;
        var startMessage = new Message(
            Guid.NewGuid(),
            now,
            new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            message,
            false,
            new Posted(false),
            IsDailyReport: isDailyReport
        );

        var thread = new Core.Models.Api.v1.Thread(
            Id: Guid.NewGuid(),
            Title: title,
            StartMessage: startMessage,
            LastMessage: startMessage,
            CreatedTimestamp: now,
            ModifiedTimestamp: now,
            FeatureConfig: null,  // will be populated when reasoning loop is created
            Source: source,
            IncidentSource: incidentSource,
            Type: threadType,
            IncidentDetails: incidentDetails
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
            ApprovalInformation: null,
            CurrentAgent: isDailyReport ? "daily_report_agent" : null,
            AllowedTools: AllowedTools
        );

        var startReasoningMessage = new ReasoningMessage(
            Id: Guid.NewGuid(),
            AgentContextId: agentContext.Id,
            Role: ReasoningMessageRoleEnum.System,
            SerializedChatMessage: JsonSerializer.Serialize(new ChatMessage(ChatRole.System, message)));

        var agentChatHistory = new AgentChatHistory(AgentContextId: agentContext.Id, ReasoningMessageIds: new List<Guid> { startReasoningMessage.Id });

        await _repository.CreateThreadAsync(thread);
        if (thread.StartMessage != null)
        {
            await _repository.AddMessageAsync(thread.Id, thread.StartMessage);
        }

        await _agentOutboundCommunicationService.NotifyThreadEvent(thread.Id, thread);
        if (thread.StartMessage != null)
        {
            await _agentOutboundCommunicationService.NotifyGenericAgentMessage(thread.Id, thread.StartMessage, null);
        }

        await _repository.CreateAgentContextAsync(agentContext);
        (thread, agentContext) = await UpdateAgentModeIfNeed(thread, agentContext, overrideAgentMode);

        await _repository.CreateReasoningMessageAsync(startReasoningMessage);

        await _repository.CreateAgentChatHistoryAsync(agentChatHistory);

        return (thread, agentContext);
    }

    public async Task<Core.Models.Api.v1.Thread> CreateAndProcessIncidentThread(string title, string message, IncidentSource incidentSource, List<IncidentDiscussion> discussions, bool defaultHandler = true)
    {
        (var thread, var agentContext) = await CreateAgentThread(
            title: title,
            message: message,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.Incident,
            incidentSource: incidentSource
        );

        await AddNewDiscussionsToIncidentThread(thread.Id, discussions);

        // add reasoning logic
        var reasoningMessage = "I would now investigate the issue and take necessary actions to automatically remediate issues accepting all next steps. I would try to be as autonomous as possible.";
        await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
            Guid.NewGuid(),
            agentContext.Id,
            ReasoningMessageRoleEnum.System,
            reasoningMessage
        ));

        var agentMessage = $"**Detected the incident**. I'm starting to investigate and see how I can help.";
        await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

        await ProcessAlertMessageAsync(new ThreadMessage(
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage?.Id ?? new Guid(),
            Message: "",
            UserId: "incident-system",
            DisplayName: incidentSource.IncidentType.ToString(),
            Timestamp: DateTime.UtcNow
        ), defaultHandler);

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

    /// <summary>
    /// Update agent to read-only if the thread is in Test mode.
    /// </summary>
    /// <param name="thread">thread</param>
    /// <param name="agentContext">AgentContext</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private async Task<(Core.Models.Api.v1.Thread, Core.Models.Api.v1.AgentContext)> UpdateAgentModeIfNeed(Core.Models.Api.v1.Thread thread, Core.Models.Api.v1.AgentContext agentContext, string overrideAgentMode)
    {
        //If is Prod thread, mode can be override by filter
        //If is Test thread, change mode to read-only
        if (thread.Type == ThreadType.Prod && !string.IsNullOrEmpty(overrideAgentMode))
        {
            _logger.LogInternalInformation($"[InboundCommunicationService]UpdateAgentModeIfNeed.ThreadId:{thread.Id}, ThreadType:{thread.Type}, RequestedAgentMode:{overrideAgentMode}");
            //In PROD, it can override thread by incidentFilter with any types
            return await ValidateAndUpdateAgentMode(thread, agentContext, overrideAgentMode, false);
        }
        else if (thread.Type == ThreadType.Test)
        {
            _logger.LogInternalInformation($"[InboundCommunicationService]UpdateAgentModeIfNeed.ThreadId:{thread.Id}, ThreadType:{thread.Type}, RequestedAgentMode:{AgentModes.ReadOnly}");
            return await ValidateAndUpdateAgentMode(thread, agentContext, AgentModes.ReadOnly, true);
        }
        else
        {
            return (thread, agentContext);
        }
    }

    /// <summary>
    /// This is to validate requested agent type based on thread type
    /// </summary>
    /// <param name="thread">Agent Thread</param>
    /// <param name="agentContext">Agent Context</param>
    /// <param name="requestedMode">Requested agent mode</param>
    /// <param name="isUpdateUponGlobalDefaultMode">True will validate against globalDefaultMode(only lower priviliage than it will be allowed). False will just check if requestedAgentMode is available</param>
    /// <returns>Updated Thread and Agent Context</returns>
    /// <exception cref="InvalidOperationException">Throw exception if validation failed</exception>
    private async Task<(Core.Models.Api.v1.Thread, Core.Models.Api.v1.AgentContext)> ValidateAndUpdateAgentMode(Core.Models.Api.v1.Thread thread, Core.Models.Api.v1.AgentContext agentContext, string requestedMode, bool isUpdateUponGlobalDefaultMode = true)
    {
        _logger.LogInternalInformation($"[InboundCommunicationService]Updating AgentMode with ThreadId:{thread.Id},RequestedMode: {requestedMode}");
        bool isValidChange = true;
        string errorMessage = "";
        if (isUpdateUponGlobalDefaultMode)
        {
            string globalDefaultMode = _actionSettings.Mode.ToString() ?? AgentModes.Review;
            isValidChange = AgentModes.IsValidModeChange(globalDefaultMode, requestedMode);

            string validationError = AgentModes.GetValidationErrorMessage(globalDefaultMode);
            errorMessage = isValidChange ? string.Empty : $"[InboundCommunicationService]Cannot change thread mode to ReadOnly from {globalDefaultMode}. Details: {validationError}";
        }
        else
        {
            isValidChange = AgentModes.IsModeValid(requestedMode);
            errorMessage = isValidChange ? string.Empty : $"[InboundCommunicationService]Request Agent mode: {requestedMode} does not exist";
        }

        if (!isValidChange && !string.IsNullOrEmpty(errorMessage))
        {
            _logger.LogInternalError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
        var updatedThread = await _repository.UpdateThreadAgentModeAsync(thread.Id, requestedMode);
        var updatedAgentContext = await _repository.GetAgentContextAsync(agentContext.Id, thread.Id);

        if (updatedThread == null)
        {
            errorMessage = $"[InboundCommunicationService]Failed to update thread {thread.Id} to {requestedMode} mode.";
            _logger.LogInternalError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        if (updatedAgentContext == null)
        {
            errorMessage = $"[InboundCommunicationService]Failed to get agent context.";
            _logger.LogInternalError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
        _logger.LogInternalInformation($"[InboundCommunicationService]Updated AgentMode for Thread and AgentContext");
        return (updatedThread, updatedAgentContext);
    }
}
