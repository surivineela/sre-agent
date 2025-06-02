// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace Agent.Runtime.MetaAgent;

public sealed class MetaAgent : IAgent
{
    private readonly ThreadService _threadService;
    private readonly IThreadRepository _threadRepository;
    private readonly AsyncReaderWriterLock _lock = new();

    private readonly IChatClient _chatClient;
    private readonly ILogger<MetaAgent> _log;

    private readonly IAgentsFactory _agentsFactory;


    public MetaAgent(
        [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient,
        IAgentsFactory agentsFactory,
        ILogger<MetaAgent> logger,
        ThreadService threadService,
        IThreadRepository threadRepository
        )
    {
        _chatClient = chatClient;
        _threadService = threadService;
        _threadRepository = threadRepository;
        _log = logger;

        _agentsFactory = agentsFactory;
        _log.LogInternalInformation("Loading agent factory of type: {AgentFactoryType}", _agentsFactory.GetType());
    }

    public async IAsyncEnumerable<ChatResponseUpdate> ProcessUserMessageStream(AgentContext agentContext, AgentChatHistory agentChatHistory)
    {
        var lastUserMessage = await _threadService.GetLastUserMessage(agentContext.ThreadId);
        _log.LogExternalInformation("[ChatThreadId {threadId}] Processing user message: {Message}", agentContext.ThreadId, lastUserMessage);
        using var _ = await _lock.AcquireWriterAsync();

        Guid threadGuid = agentContext.ThreadId;
        string systemPrompt = _agentsFactory.GetMetaAgentSystemPrompt();
        var _aiTools = _agentsFactory.GetSubAgentsAITools(threadGuid, agentContext);

        var chatHistoryReasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_threadRepository);
        var chatHistory = chatHistoryReasoningMessages.GetChatMessages();
        var lastMessageAppended = chatHistory.LastOrDefault()?.Text.Equals(lastUserMessage, StringComparison.Ordinal) ?? false;
        if (!lastMessageAppended && !string.IsNullOrEmpty(lastUserMessage))
        {
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, lastUserMessage));
        }

        // Always use the latest System Prompt in case we have some urgent fix to patch for the old chat history.
        if (chatHistory[0].Role == ChatRole.System)
        {
            chatHistory[0] = new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, systemPrompt);
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
        await foreach (var response in streamResponses)
        {
            bufferedResponses.Add(response);
            agentResponse.Append(response.Text);
            // Record final agentResponse, ignore duplicate STOP commands from model
            if (response.FinishReason == ChatFinishReason.Stop && !hasRecordedResposne)
            {
                ChatResponse chatResponse = new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, agentResponse.ToString())
                );
                await chatResponse.UpdateAgentChatHistoryAsync(agentChatHistory, _threadRepository, agentContext.Id);
                hasRecordedResposne = true;
            }
            yield return response;
        }
    }

    public async Task<string> ProcessUserMessageAsync(AgentContext agentContext, AgentChatHistory agentChatHistory)
    {
        var lastUserMessage = await _threadService.GetLastUserMessage(agentContext.ThreadId);
        _log.LogExternalInformation("[ChatThreadId {threadId}] Processing user message: {Message}", agentContext.ThreadId, lastUserMessage);
        using var _ = await _lock.AcquireWriterAsync();

        Guid threadGuid = agentContext.ThreadId;

        var chatHistoryReasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_threadRepository);
        var chatHistory = chatHistoryReasoningMessages.GetChatMessages();
        var lastMessageAppended = chatHistory.LastOrDefault()?.Text.Equals(lastUserMessage, StringComparison.Ordinal) ?? false;
        if (!lastMessageAppended && !string.IsNullOrEmpty(lastUserMessage))
        {
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, lastUserMessage));
        }

        try
        {
            var response = await GetModelResponse(agentContext, threadGuid, chatHistory);

            await response.UpdateAgentChatHistoryAsync(agentChatHistory, _threadRepository, agentContext.Id);
            return response.Messages.Last().Text;
        }
        catch (System.ClientModel.ClientResultException ex) when (ex.Message.Contains("HTTP 400 (content_filter)"))
        {
            _log.LogInternalError(ex, "An error occurred while processing the user message.");
            return ex.Message;

        }
    }

    public async Task<ChatResponse> GetModelResponse(AgentContext agentContext, Guid threadGuid, List<ChatMessage> chatHistory)
    {
        string systemPrompt = _agentsFactory.GetMetaAgentSystemPrompt();
        var _aiTools = _agentsFactory.GetSubAgentsAITools(threadGuid, agentContext);

        // Always use the latest System Prompt in case we have some urgent fix to patch for the old chat history.
        if (chatHistory[0].Role == ChatRole.System)
        {
            chatHistory[0] = new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, systemPrompt);
        }

        var response = await ChatClientHelper.ExecuteWithRetryAsync(
            async () => await _chatClient.GetResponseAsync(
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
                }
            ),
            _log, 10);
        return response;
    }
}
