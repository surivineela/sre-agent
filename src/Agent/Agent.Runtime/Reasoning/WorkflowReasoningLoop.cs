// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.AgentMemory;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// A specialized ReasoningLoop that delegates workflow operations to WorkflowOrchestrator.
/// This maintains compatibility with the existing ReasoningLoop interface while providing
/// workflow orchestration capabilities for RCARouterAgent.
/// Designed to be easily removable when no longer needed.
/// </summary>
public class WorkflowReasoningLoop : ReasoningLoop
{
    private readonly WorkflowOrchestrator _workflowOrchestrator;
    private readonly ILogger<WorkflowReasoningLoop> _logger;

    public WorkflowReasoningLoop(
        WorkflowOrchestrator workflowOrchestrator,
        ILoggerFactory loggerFactory,
        IChatClientProvider chatClientProvider,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IStreamingMessageRepository streamingMessageRepository,
        Agent<AgentContext> defaultStartingAgent,
        Agent<AgentContext> startingAgent,
        IThreadRepository threadRepository,
        AgentContext context,
        IToolFactory<AgentContext> toolFactory,
        ActionSettings actionSettings,
        Tracer tracer,
        CustomerLogger customerLogger,
        IAgentProvider<AgentContext> agentProvider,
        bool enableReasoningDebugOutput,
        ISearchEndpointService searchEndpointService,
        SearchHelper searchHelper,
        IAgentMemoryClient agentMemoryClient,
        ISearchIndexService searchIndexService,
        AgentMemorySettings agentMemorySettings,
        FeatureConfigModel featureConfig,
        IAgentRuntimeModifier<AgentContext> agentRuntimeModifier,
        ISkillRegistry skillRegistry,
        IncidentManagementSettings incidentManagementSettings,
        CoreSettings coreSettings,
        bool modeSwitchEnabled,
        IToolOutputProcessService toolOutputProcessService,
        IAgentFileStorageService agentFileStorageService,
        IHostEnvironment hostEnvironment,
        IAmbientContextProvider ambientContextProvider)
        : base(
            loggerFactory: loggerFactory,
            chatClientProvider: chatClientProvider,
            outboundCommunicationService: outboundCommunicationService,
            streamingMessageRepository: streamingMessageRepository,
            defaultStartingAgent: defaultStartingAgent,
            startingAgent: startingAgent,
            threadRepository: threadRepository,
            context: context,
            toolFactory: toolFactory,
            actionSettings: actionSettings,
            tracer: tracer,
            customerLogger: customerLogger,
            agentProvider: agentProvider,
            enableReasoningDebugOutput: enableReasoningDebugOutput,
            searchEndpointService: searchEndpointService,
            searchHelper: searchHelper,
            agentMemoryClient: agentMemoryClient,
            searchIndexService: searchIndexService,
            agentMemorySettings: agentMemorySettings,
            featureConfig: featureConfig,
            agentRuntimeModifier: agentRuntimeModifier,
            modeSwitchEnabled: modeSwitchEnabled,
            skillRegistry: skillRegistry,
            toolOutputProcessService: toolOutputProcessService,
            agentFileStorageService: agentFileStorageService,
            hostEnvironment: hostEnvironment,
            ambientContextProvider: ambientContextProvider)
    {
        _workflowOrchestrator = new WorkflowOrchestrator(
            loggerFactory: loggerFactory,
            chatClientProvider: chatClientProvider,
            outboundCommunicationService: outboundCommunicationService,
            threadRepository: threadRepository,
            context: context,
            agentProvider: agentProvider,
            toolFactory: toolFactory,
            tracer: tracer,
            incidentManagementSettings: incidentManagementSettings,
            coreSettings: coreSettings,
            skillRegistry: skillRegistry);

        _logger = loggerFactory.CreateLogger<WorkflowReasoningLoop>();
    }

    public new async Task LoadChatHistoryAsync()
    {
        _logger.LogInternalInformation("WorkflowReasoningLoop: Loading chat history via WorkflowOrchestrator");
        await _workflowOrchestrator.LoadChatHistoryAsync();
    }

    public override async Task AppendNewUserMessageAsync(ChatMessage msg, ConversationModifierEnum? conversationModifier = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("WorkflowReasoningLoop: Processing new user message via WorkflowOrchestrator");
        // Note: WorkflowOrchestrator doesn't currently support conversation modifiers
        // For now, pass through without modifier processing
        await _workflowOrchestrator.AppendNewUserMessageAsync(msg, cancellationToken);
    }

    public override async Task AppendFunctionCallMessagesAsync(List<ChatMessage> msgs, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("WorkflowReasoningLoop: AppendFunctionCallMessagesAsync called (delegating to WorkflowOrchestrator)");
        await _workflowOrchestrator.AppendFunctionCallMessagesAsync(msgs, cancellationToken);
    }

    public override async Task AppendNewApprovalMessageAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("WorkflowReasoningLoop: AppendNewApprovalMessageAsync called (delegating to WorkflowOrchestrator)");
        await _workflowOrchestrator.AppendNewApprovalMessageAsync(approval, cancellationToken);
    }

    public override Task<IEnumerable<ChatMessage>> ExportChatHistoryAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("WorkflowReasoningLoop: Exporting chat history via WorkflowOrchestrator");
        return _workflowOrchestrator.ExportChatHistoryAsync(cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logger.LogInternalInformation("WorkflowReasoningLoop: Disposing WorkflowOrchestrator");
            _workflowOrchestrator?.Dispose();
        }
        base.Dispose(disposing);
    }
}



