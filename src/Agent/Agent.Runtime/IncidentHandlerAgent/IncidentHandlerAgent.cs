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
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

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
        ActionSettings actionSettings,
        IAgentOutboundCommunicationService agentOutboundCommunicationService
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
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
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
        bool isProcessingToolCalls = false;
        var orchestrationInstanceId = await _threadService.GetOrchestrationInstanceId(agentContext.ThreadId);

        _logger.LogInternalInformation(
            "[IncidentHandlerAgent] ProcessIncidentStream: Starting to process streaming responses for ThreadId: {ThreadId}",
            threadGuid);

        await foreach (var response in streamResponses)
        {
            bufferedResponses.Add(response);
            agentResponse.Append(response.Text);

            // Handle tool call notifications during streaming
            if (response.FinishReason == ChatFinishReason.ToolCalls && !isProcessingToolCalls)
            {
                isProcessingToolCalls = true;
                
                // Send initial "processing tools" notification
                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                    threadGuid,
                    orchestrationInstanceId,
                    new ChatMessage(ChatRole.Assistant, "🔄 Processing tools and gathering information...")
                );
            }

            // Handle individual function calls in the streaming response
            if (response.Contents != null)
            {
                foreach (var content in response.Contents)
                {
                    if (content is FunctionCallContent functionCall)
                    {
                        var toolName = functionCall.Name;
                        var toolDescription = GetToolCallDescription(toolName);
                        
                        await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                            threadGuid,
                            orchestrationInstanceId,
                            new ChatMessage(ChatRole.Assistant, $"🔧 {toolDescription}")
                        );

                        _logger.LogInternalInformation(
                            "[IncidentHandlerAgent] ProcessIncidentStream: Sent tool call notification for {ToolName} in thread {ThreadId}",
                            toolName, threadGuid);
                    }
                }
            }

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

            //await response.UpdateAgentChatHistoryAsync(agentChatHistory, _threadRepository, agentContext.Id);

            
            if (response != null)
            {
                var orchestrationInstanceId = agentContext != null ? await _threadService.GetOrchestrationInstanceId(agentContext.ThreadId) : string.Empty;

                foreach (var message in response.Messages)
                {
                    await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                   threadGuid,
                   orchestrationInstanceId,
                   message);
                }
            }

            _logger.LogInternalInformation(
                "[IncidentHandlerAgent] ProcessIncidentAsync: Successfully processed incident for AgentContextId: {AgentContextId}, ThreadId: {ThreadId}",
                agentContext != null ? agentContext.Id : Guid.Empty, threadGuid);

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
                async () => await GetResponseWithStreamingNotificationsAsync(
                    chatHistory,
                    selectedTools,
                    threadGuid,
                    agentContext
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
    /// Gets model response with streaming tool call notifications to the UI
    /// </summary>
    private async Task<ChatResponse> GetResponseWithStreamingNotificationsAsync(
        List<ChatMessage> chatHistory,
        List<AITool>? selectedTools,
        Guid threadGuid,
        AgentContext agentContext)
    {
        var streamingResponse = _chatClient.GetStreamingResponseAsync(
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
            });

        var orchestrationInstanceId = await _threadService.GetOrchestrationInstanceId(agentContext.ThreadId);
        var responseMessages = new List<ChatMessage>();
        var currentMessageBuilder = new StringBuilder();
        var currentFunctionCalls = new List<FunctionCallContent>();
        
        bool isProcessingToolCalls = false;

        await foreach (var update in streamingResponse)
        {
            // Check if we're starting to process tool calls
            if (update.FinishReason == ChatFinishReason.ToolCalls && !isProcessingToolCalls)
            {
                isProcessingToolCalls = true;
                
                // Send initial "processing tools" notification
                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                    threadGuid,
                    orchestrationInstanceId,
                    new ChatMessage(ChatRole.Assistant, "🔄 Processing tools and gathering information...")
                );
            }

            // Collect text content
            if (!string.IsNullOrEmpty(update.Text))
            {
                currentMessageBuilder.Append(update.Text);
            }

            // Handle function calls in the streaming response
            if (update.Contents != null)
            {
                foreach (var content in update.Contents)
                {
                    if (content is FunctionCallContent functionCall)
                    {
                        currentFunctionCalls.Add(functionCall);
                        
                        // Send notification for each tool call
                        var toolName = functionCall.Name;
                        var toolDescription = GetToolCallDescription(toolName);
                        
                        await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                            threadGuid,
                            orchestrationInstanceId,
                            new ChatMessage(ChatRole.Assistant, $"🔧 {toolDescription}")
                        );

                        _logger.LogInternalInformation(
                            "[IncidentHandlerAgent] GetResponseWithStreamingNotifications: Sent tool call notification for {ToolName} in thread {ThreadId}",
                            toolName, threadGuid);
                    }
                }
            }
        }

        // Build the final response
        var finalMessage = new ChatMessage(ChatRole.Assistant, currentMessageBuilder.ToString());
        if (currentFunctionCalls.Count > 0)
        {
            // Add function calls to the message contents
            var allContents = new List<AIContent> { new TextContent(currentMessageBuilder.ToString()) };
            allContents.AddRange(currentFunctionCalls);
            finalMessage = new ChatMessage(ChatRole.Assistant, allContents);
        }

        responseMessages.Add(finalMessage);

        return new ChatResponse(responseMessages);
    }

    /// <summary>
    /// Gets a user-friendly description for tool calls to display in the UI
    /// </summary>
    private string GetToolCallDescription(string toolName)
    {
        return toolName switch
        {
            // Kusto/Query tools
            "execute_kusto_query_on_cluster" => "Executing Kusto query to analyze data...",
            "kusto_query" => "Running data analysis query...",
            
            // Azure CLI tools
            "run_az_cli_read_commands" => "Running Azure CLI Read command...",
            "run_az_cli_commands" => "Executing Azure CLI operations...",
            
            // Resource discovery
            "list_resources_by_type" => "Discovering Azure resources...",
            "get_resource_details" => "Getting resource details...",
            
            // Chart/Visualization tools
            "plot_scatter" => "Creating scatter plot visualization...",
            "plot_time_series_data" => "Generating time series chart...",
            "plot_pie_chart" => "Creating pie chart...",
            "plot_bar_chart" => "Generating bar chart...",
            
            // Transfer tools
            var transfer when transfer.StartsWith("transfer_to_") => "Transferring to specialized agent...",
            
            // Default
            _ => $"Executing {toolName.Replace("_", " ")}..."
        };
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
