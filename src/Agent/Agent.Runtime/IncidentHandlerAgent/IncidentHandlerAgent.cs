// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.IncidentHandlerAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;


namespace Agent.Runtime.MetaAgent;

public sealed class IncidentHandlerAgent : IIncidentHandlerAgent
{
    private readonly ThreadService _threadService;
    private readonly IThreadRepository _threadRepository;
    private readonly AsyncReaderWriterLock _lock = new();

    private readonly IChatClient _chatClient;
    private readonly ILogger<IncidentHandlerAgent> _logger;
    private readonly Tracer _tracer;

    private readonly IAgentsFactory _agentsFactory;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly ActionSettings _actionSettings;

    public IncidentHandlerAgent(
        [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient,
        IAgentsFactory agentsFactory,
        IToolFactory<AgentContext> toolFactory,
        ILogger<IncidentHandlerAgent> logger,
        Tracer tracer,
        ThreadService threadService,
        IThreadRepository threadRepository,
        ActionSettings actionSettings
        )
    {
        _chatClient = chatClient;
        _threadService = threadService;
        _threadRepository = threadRepository;
        _logger = logger;
        _tracer = tracer;

        _agentsFactory = agentsFactory;
        _toolFactory = toolFactory;
        _logger.LogInternalInformation(
            "IncidentHandlerAgent: Constructor invoked. Loading agent factory of type: {AgentFactoryType}",
            _agentsFactory.GetType());
        _actionSettings = actionSettings;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> ProcessIncidentStream(AgentContext agentContext, AgentChatHistory agentChatHistory)
    {
        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] ProcessIncidentStream: Invoked for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
            agentContext.Id, agentContext.ThreadId);

        var lastUserMessage = await _threadService.GetLastUserMessage(agentContext.ThreadId);
        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] ProcessIncidentStream: Retrieved last user message for ThreadId: {ThreadId}. Message: {Message}",
            agentContext.ThreadId, lastUserMessage);

        using var _ = await _lock.AcquireWriterAsync();

        Guid threadGuid = agentContext.ThreadId;
        var mode = GetModeForSystemPrompt(agentContext);
        string systemPrompt = _agentsFactory.GetIncidentHandlerAgentSystemPrompt(mode);
        var _aiTools = _agentsFactory.GetSubAgentsAITools(threadGuid, agentContext);

        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] ProcessIncidentStream: Retrieved {ToolCount} AI tools for ThreadId: {ThreadId}",
            _aiTools?.Count ?? 0, threadGuid);

        var chatHistoryReasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_threadRepository);
        var chatHistory = chatHistoryReasoningMessages.GetChatMessages();
        var lastMessageAppended = chatHistory.LastOrDefault()?.Text.Equals(lastUserMessage, StringComparison.Ordinal) ?? false;
        if (!lastMessageAppended && !string.IsNullOrEmpty(lastUserMessage))
        {
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, lastUserMessage));
            _logger.LogInternalInformation(
                "[IncidentHandlerAgent] ProcessIncidentStream: Appended last user message to chat history for ThreadId: {ThreadId}",
                threadGuid);
        }

        // Always use the latest System Prompt in case we have some urgent fix to patch for the old chat history.
        if (chatHistory[0].Role == ChatRole.System)
        {
            //chatHistory[0] = new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, systemPrompt);
            chatHistory.Insert(1, new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, systemPrompt));
            _logger.LogInternalInformation(
                "[IncidentHandlerAgent] ProcessIncidentStream: Updated system prompt in chat history for ThreadId: {ThreadId}",
                threadGuid);
        }

        List<ChatResponseUpdate> bufferedResponses = new();

        // exceptions should be handled by caller due to yield return
        var streamResponses = _chatClient.GetStreamingResponseAsync(
            chatHistory,
            new ChatOptions
            {
                Tools = _aiTools,
                ToolMode = ChatToolMode.Auto,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    //["AllowParallelToolCalls"] = false,
                },
                Temperature = 0.7f,
            });

        StringBuilder agentResponse = new StringBuilder();
        bool hasRecordedResposne = false;

        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] ProcessIncidentStream: Starting to process streaming responses for ThreadId: {ThreadId}",
            threadGuid);

        await foreach (var response in streamResponses)
        {
            bufferedResponses.Add(response);
            agentResponse.Append(response.Text);

            if (response.FinishReason == ChatFinishReason.Stop && !hasRecordedResposne)
            {
                ChatResponse chatResponse = new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, agentResponse.ToString())
                );
                await chatResponse.UpdateAgentChatHistoryAsync(agentChatHistory, _threadRepository, agentContext.Id);
                hasRecordedResposne = true;

                _logger.LogInternalInformation(
                    "[IncidentHandlerAgent] ProcessIncidentStream: Final agent response recorded for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                    agentContext.Id, threadGuid);
            }
            yield return response;
        }

        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] ProcessIncidentStream: Completed streaming responses for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}, ResponseCount: {ResponseCount}",
            agentContext.Id, threadGuid, bufferedResponses.Count);
    }

    public async Task<string> ProcessIncidentAsync(AgentContext agentContext, AgentChatHistory agentChatHistory)
    {
        using var span = _tracer.StartSpan(TraceOperationName.IncidentProcessMessage, SpanKind.Internal);
        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] ProcessIncidentAsync: Invoked for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
            agentContext.Id, agentContext.ThreadId);

        var lastUserMessage = await _threadService.GetLastUserMessage(agentContext.ThreadId);
        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] ProcessIncidentAsync: Retrieved last user message for ThreadId: {ThreadId}. Message: {Message}",
            agentContext.ThreadId, lastUserMessage);

        span.SetAttribute(TraceAttribute.OperationName, TraceOperationName.IncidentProcessMessage);
        span.SetAttribute(TraceAttribute.ThreadId, agentContext.ThreadId.ToString());
        span.SetAttribute(TraceAttribute.MessageContent, lastUserMessage ?? string.Empty);

        using var _ = await _lock.AcquireWriterAsync();

        Guid threadGuid = agentContext.ThreadId;

        var chatHistoryReasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_threadRepository);
        var chatHistory = chatHistoryReasoningMessages.GetChatMessages();
        var lastMessageAppended = chatHistory.LastOrDefault()?.Text.Equals(lastUserMessage, StringComparison.Ordinal) ?? false;
        if (!lastMessageAppended && !string.IsNullOrEmpty(lastUserMessage))
        {
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, lastUserMessage));
            _logger.LogInternalInformation(
                "[IncidentHandlerAgent] ProcessIncidentAsync: Appended last user message to chat history for ThreadId: {ThreadId}",
                threadGuid);
        }        try
        {
            _logger.LogInternalInformation(
                "[IncidentHandlerAgent] ProcessIncidentAsync: Calling GetModelResponse for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                agentContext.Id, threadGuid);

            using var generationSpan = _tracer.StartSpan(TraceOperationName.ModelGeneration, SpanKind.Internal, span);
            generationSpan.SetAttribute(TraceAttribute.ThreadId, agentContext.ThreadId.ToString());
            generationSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.ModelGeneration);
            generationSpan.SetAttribute(TraceAttribute.ModelInput, ChatMessageFormatter.FormatChatMessages(chatHistory));

            var response = await GetModelResponse(agentContext, threadGuid, chatHistory);

            generationSpan.SetAttribute(TraceAttribute.ModelOutput, ChatMessageFormatter.FormatChatMessages(response?.Messages ?? []));
            generationSpan.SetAttribute(TraceAttribute.ModelInputTokensCount, response?.Usage?.InputTokenCount?.ToString() ?? string.Empty);
            generationSpan.SetAttribute(TraceAttribute.ModelOutputTokensCount, response?.Usage?.OutputTokenCount?.ToString() ?? string.Empty);
            generationSpan.SetAttribute(TraceAttribute.ModelTotalTokensCount, response?.Usage?.TotalTokenCount?.ToString() ?? string.Empty);
            generationSpan.SetAttribute(TraceAttribute.ModelTemperature, "0.7");

            await response.UpdateAgentChatHistoryAsync(agentChatHistory, _threadRepository, agentContext.Id);

            _logger.LogInternalInformation(
                "[IncidentHandlerAgent] ProcessIncidentAsync: Successfully processed incident for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                agentContext.Id, threadGuid);

            return response?.Messages?.LastOrDefault()?.Text ?? string.Empty;
        }
        catch (System.ClientModel.ClientResultException ex) when (ex.Message.Contains("HTTP 400 (content_filter)"))
        {
            _logger.LogInternalError(
                ex,
                "[IncidentHandlerAgent] ProcessIncidentAsync: Content filter error occurred while processing user message for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                agentContext.Id, threadGuid);
            return ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "[IncidentHandlerAgent] ProcessIncidentAsync: Exception occurred for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                agentContext.Id, threadGuid);
            throw;
        }
    }

    public async Task<ChatResponse> GetModelResponse(AgentContext agentContext, Guid threadGuid, List<ChatMessage> chatHistory)
    {
        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] GetModelResponse: Invoked for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}, AgentMode: {AgentMode}",
            agentContext.Id, threadGuid, agentContext.AgentMode);

        var mode = GetModeForSystemPrompt(agentContext);
        string systemPrompt = _agentsFactory.GetIncidentHandlerAgentSystemPrompt(mode);
        var _aiTools = _agentsFactory.GetSubAgentsAITools(threadGuid, agentContext);
        
        // Updated to pass the agent mode to tool factory
        var _toolFactoryTools = agentContext.AllowedTools != null 
            ? agentContext.AllowedTools.Select(x => (AITool)_toolFactory.GetTool(x, threadGuid, mode)).ToList() 
            : new List<AITool>();

        var selectedTools = agentContext.AllowedTools != null && agentContext.AllowedTools.Count > 0
            ? _aiTools.Where(x => x.Name != null && agentContext.AllowedTools.Contains(x.Name))?.ToList().Concat(_toolFactoryTools).ToList()
            : _aiTools;

        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] GetModelResponse: Selected {ToolCount} tools for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
            selectedTools?.Count ?? 0, agentContext.Id, threadGuid);

        // Always use the latest System Prompt in case we have some urgent fix to patch for the old chat history.
        if (chatHistory[0].Role != ChatRole.System || !string.Equals(chatHistory[0].Text, systemPrompt, StringComparison.OrdinalIgnoreCase))
        {
            chatHistory.Insert(0, new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, systemPrompt));
            _logger.LogInternalInformation(
           "[IncidentHandlerAgent] GetModelResponse: Updated system prompt in chat history for ThreadId: {ThreadId}",
           threadGuid);
        }

        try
        {
            _logger.LogInternalInformation(
                "[IncidentHandlerAgent] GetModelResponse: Calling ChatClientHelper.ExecuteWithRetryAsync for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                agentContext.Id, threadGuid);

            var response = await ChatClientHelper.ExecuteWithRetryAsync(
                async () => await _chatClient.GetResponseAsync(
                    chatHistory,
                    new ChatOptions
                    {
                        Tools = selectedTools,
                        ToolMode = ChatToolMode.Auto,
                        AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            //["AllowParallelToolCalls"] = false,
                        },
                        Temperature = 0.7f,
                    }
                ),
                _logger, 10);

            _logger.LogInternalInformation(
                "[IncidentHandlerAgent] GetModelResponse: Successfully received model response for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                agentContext.Id, threadGuid);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "[IncidentHandlerAgent] GetModelResponse: Exception occurred for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                agentContext.Id, threadGuid);
            throw;
        }
    }

    /// <summary>
    /// Get agentMode from agentContext, if not exist fallback to defaultGlobalAgentMode
    /// </summary>
    /// <param name="context">AgentContext</param>
    /// <returns></returns>
    private string? GetModeForSystemPrompt(AgentContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.AgentMode))
        {
            return context.AgentMode;
        }
        else
        {
            return _actionSettings.Mode.ToString();
        }
    }
}
