// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.MetaAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Runtime.Services;
using Agent.Plugins.Definitions;
using Agent.Core.Helpers;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Plugins;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents;

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

    public async Task<(Core.Models.Api.v1.Thread, Core.Models.Api.v1.ThreadContext)> CreateAgentThread(
        string title,
        string message,
        AgentTypeEnum agentTypeEnum,
        ThreadSource source = ThreadSource.Agent)
    {
        return await CreateThread(title, message, source, agentTypeEnum);
    }

    public async Task<Core.Models.Api.v1.Thread> CreateAlertThreadWithTeams(
        string title,
        string message,
        AgentTypeEnum agentTypeEnum,
        ThreadSource source = ThreadSource.Alert)
    {
        var outboundConfig = new OutboundConfiguration { Teams = new Teams { Enabled = true } };
        (var thread, var threadContext) = await CreateThread(title, message, source, agentTypeEnum, outboundConfig);
        await _teamsPlugin.CreateTeamsThread(thread.Id.ToString(), thread.StartMessage.Text, thread.StartMessage.Id.ToString());

        return thread;
    }

    public async Task ProcessAlertMessageAsync(ThreadMessage message)
    {
        // TODO(jianbosun) - this is a placeholder for the alert message processing
        // In the future, we may want to add some logic here to handle alert messages differently
        await ProcessUserMessageAsync(message);
    }

    public async Task<Guid> AppendAgentImageMessage(ThreadContext threadContext, string message)
    {
        if (threadContext == null || threadContext.ThreadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadContext));
        }

        return await _sinkService.SinkAgentMessageAsync(threadContext, message, isImageContent: true);
    }

    public async Task<InboundServiceResponse> ProcessUserMessageAsync(ThreadMessage message)
    {
        try
        {
            string orchestrationInstanceId = "";
            Guid responseMessageId = Guid.Empty;

            // Check if an orchestration already exists for this thread
            ThreadContext threadContext = await _repository.GetThreadContextAsync(message.ThreadId);
            orchestrationInstanceId = threadContext != null ? await _threadService.GetOrchestrationInstanceId(threadContext) : orchestrationInstanceId;

            // we don't need to sink user message if the message is the start message
            var thread = await _repository.GetThreadAsync(message.ThreadId);
            if (message?.MessageId != thread?.StartMessage?.Id)
            {
                await _sinkService.SinkUserMessageAsync(threadContext, message);
            }

            if (!string.IsNullOrEmpty(orchestrationInstanceId))
            {

                var existingOrchestration = await _durableTaskClient.GetInstanceAsync(orchestrationInstanceId,
                    getInputsAndOutputs: true, CancellationToken.None);
                // Check for failed orchestrations and clean them if needed
                bool cleaned = await _threadService.CleanOrchestration(
                    threadContext,
                    orchestrationInstanceId,
                    existingOrchestration);

                // If the orchestration was cleaned, get the updated orchestration ID (might be empty now)
                if (cleaned)
                {
                    orchestrationInstanceId = await _threadService.GetOrchestrationInstanceId(threadContext);
                }
            }

            if (string.IsNullOrEmpty(orchestrationInstanceId))
            {
                // No existing orchestration, create a new one
                _logger.LogInformation("No existing orchestration for thread: {ThreadId}", message.ThreadId);

                string agentResponse = string.Empty;
                bool isComplete = false;

                if (AgentTypeHelper.IsScannerAgent(threadContext.AgentTypeEnum))
                {
                    threadContext.ResumeThreadContext();
                    await _repository.UpdateThreadContextAsync(threadContext);

                    ScannerSubAgent scannerSubAgent = null;
                    switch (threadContext.AgentTypeEnum)
                    {
                        case AgentTypeEnum.CVE:
                            scannerSubAgent = new CVEAgent(_chatClient, _graphDbPlugin, _githubIssuePlugin, _sinkService, _repository);
                            break;
                        case AgentTypeEnum.SourceCode:
                            scannerSubAgent = new SourceCodeAgent(_chatClient, _graphDbPlugin, _sinkService, _repository);
                            break;
                        default:
                            throw new NotSupportedException($"Scanner agent type {threadContext.AgentTypeEnum} is not supported.");
                    }

                    if (scannerSubAgent != null)
                    {
                        agentResponse = await scannerSubAgent.DoWork(threadContext, message.Message);
                    }
                }
                else
                {
                    // Process the message with MetaAgent
                    agentResponse = await _metaAgent.ProcessUserMessage(threadContext);
                }

                responseMessageId = await _sinkService.SinkAgentMessageAsync(threadContext, agentResponse);
            }
            else
            {
                // TODO (jianbosun): 
                // For now, we assume there's only 1:1 mapping for threadId and orchestrationInstanceId,
                // but we may change this to allow multiple orchestrations per thread, e.g. to choose sub-agent type in one thread as a different orchestration.
                // This will enable us for scenarios that need to share chat history with multiple orchestrations for different purposes.

                // Existing orchestration, raise an event to it
                _logger.LogInformation("Sending message to existing orchestration for thread: {ThreadId}", message.ThreadId);
                await _durableTaskClient.RaiseEventAsync(
                    orchestrationInstanceId,
                    "NewChatMessage",
                    new ChatMessage(ChatRole.User, message.Message));
            }

            return new InboundServiceResponse(message.ThreadId, responseMessageId, orchestrationInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user message for thread: {ThreadId}", message.ThreadId);
            throw;
        }
    }

    public async Task<MessageFeedback> ProcessFeedbackAsync(ThreadMessageFeedback threadMessageFeedback)
    {
        try
        {
            // Check if an orchestration already exists for this thread
            ThreadContext threadContext = await _repository.GetThreadContextAsync(threadMessageFeedback.ThreadId);

            var messageFeedback = new MessageFeedback(
                Id: threadMessageFeedback.MessageFeedbackId,
                ThreadId: threadMessageFeedback.ThreadId,
                TimeStamp: DateTime.UtcNow,
                Messages: threadContext.RecentMessages.ToList(),
                IsPositiveFeedback: threadMessageFeedback.IsPositive,
                FeedbackText: threadMessageFeedback.FeedbackText,
                RootCause: null);

            await _repository.AddOrUpdateMessageFeedbackAsync(threadMessageFeedback.ThreadId, messageFeedback);

            return messageFeedback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing feedback for thread: {ThreadId}", threadMessageFeedback.ThreadId);
            throw;
        }
    }

    private async Task<(Core.Models.Api.v1.Thread, ThreadContext)> CreateThread(
        string title,
        string message,
        ThreadSource source,
        AgentTypeEnum agentTypeEnum,
        OutboundConfiguration? outboundConfiguration = null)
    {
        var now = DateTime.UtcNow;
        var startMessage = new Message(
            Guid.NewGuid(),
            now,
            new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            message,
            false,
            new Posted(false)
        );

        var thread = new Core.Models.Api.v1.Thread(
            Id: Guid.NewGuid(),
            Title: title,
            StartMessage: startMessage,
            LastMessage: startMessage,
            CreatedTimestamp: now,
            ModifiedTimestamp: now,
            Source: source
        );

        var subAgentThread = new SubAgentThread(
            Id: Guid.NewGuid(),
            ThreadId: thread.Id,
            AgentType: agentTypeEnum
        );

        var startReasoningMessage = new ReasoningMessage(
            Id: Guid.NewGuid(),
            SubAgentThreadId: subAgentThread.Id,
            Role: ReasoningMessageRoleEnum.Assistant,
            Text: startMessage.Text,
            FunctionInvocation: null);

        // TODO - how should we share implementation with process user message and make sure fan out occurs?
        await _repository.CreateThreadAsync(thread);
        await _repository.CreateSubAgentThreadAsync(subAgentThread);
        await _repository.AddMessageAsync(thread.Id, thread.StartMessage);
        await _repository.CreateReasoningMessageAsync(startReasoningMessage);

        var threadContext = new ThreadContext(thread.Id, agentTypeEnum: agentTypeEnum, outboundConfiguration: outboundConfiguration);
        threadContext.AddMessage(thread.StartMessage);
        await _repository.AddThreadContextAsync(threadContext);

        return (thread, threadContext);
    }
}
