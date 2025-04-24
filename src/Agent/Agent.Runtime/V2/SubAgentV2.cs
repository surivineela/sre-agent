// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.V2;

/// <summary>
/// SubAgent implementation
/// </summary>
/// <typeparam name="TDefinition">SubAgent definition type</typeparam>
/// <typeparam name="TInput">Input data type</typeparam>
public class SubAgentV2<TDefinition, TInput> : ISubAgentV2 where TDefinition : ISubAgentDefinition<TInput>
{
    private readonly IChatClient _chatClient;
    private readonly List<AITool> _tools;
    private readonly IThreadRepository _threadRepository;
    private readonly AgentContext _context;

    private ChatOptions ChatOptionsWithTools => new()
    {
        Tools = _tools
    };

    public SubAgentV2(
        AgentContext agentContext,
        IChatClient chatClient,
        IToolsRepository toolsRepository,
        IThreadRepository threadRepository,
        IAgentOutboundCommunicationService outboundCommunicationService,
        ILoggerFactory loggerFactory)
    {
        _context = agentContext;

        var controlFlowPlugin = new ControlFlowV2Plugin(
            threadRepository,
            outboundCommunicationService,
            agentContext,
            loggerFactory.CreateLogger<ControlFlowV2Plugin>());

        var controlFlowPluginDef = new ControlFlowV2PluginDefinition(controlFlowPlugin);

        // standard control flow tools
        _tools = [
            AIFunctionFactory.Create(controlFlowPluginDef.StartWait),
            AIFunctionFactory.Create(controlFlowPluginDef.Complete),
            AIFunctionFactory.Create(controlFlowPluginDef.AskForUserInput),
            AIFunctionFactory.Create(controlFlowPluginDef.NotifyUser)
        ];

        _tools.AddRange(toolsRepository.ResolveTools(TDefinition.ToolSignatures));

        _chatClient = chatClient;
        _threadRepository = threadRepository;
    }

    public async Task DoWork(
        AgentChatHistory? agentChatHistory,
        bool initWithSystemPrompt = false)
    {
        // initialize chat context
        List<ChatMessage> chatMessages = [];

        if (agentChatHistory != null)
        {
            var history = (await agentChatHistory.GetReasoningMessagesAsync(_threadRepository))?.GetChatMessages();

            if (history != null)
            {
                chatMessages.AddRange(history);
            }
        }
        else
        {
            agentChatHistory = new(_context.Id, []);
        }

        // add system prompt if needed
        if (chatMessages.Count == 0 || initWithSystemPrompt)
        {
            chatMessages.Add(new(ChatRole.System, TDefinition.GetSystemPrompt(GetInputData(_context))));
        }

        // get model response
        var chatResponse = await _chatClient.GetResponseAsync(chatMessages, ChatOptionsWithTools);

        // persist responses
        var responseMessages = chatResponse.GetReasoningMessages(_context.Id);

        foreach (var reasoningMessage in responseMessages)
        {
            await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);
        }

        // update chat history
        await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, responseMessages);
    }

    protected virtual TInput? GetInputData(AgentContext agentContext)
    {
        TInput? inputData = default;

        if (!string.IsNullOrEmpty(agentContext.InputDataSerialized))
        {
            try
            {
                inputData = JsonSerializer.Deserialize<TInput>(agentContext.InputDataSerialized);
            }
            catch
            {
                // swallow exception
            }
        }

        return inputData;
    }
}

/// <summary>
/// SubAgent with no input data
/// </summary>
/// <typeparam name="TDefinition">SubAgent definition type</typeparam>
public class SubAgentV2<TDefinition>(
    AgentContext agentContext,
    IChatClient chatClient,
    ToolsRepository toolsRepository,
    IThreadRepository threadRepository,
    IAgentOutboundCommunicationService outboundCommunicationService,
    ILoggerFactory loggerFactory
) : SubAgentV2<TDefinition, object?>(
    agentContext,
    chatClient,
    toolsRepository,
    threadRepository,
    outboundCommunicationService,
    loggerFactory) where TDefinition : ISubAgentDefinition
{
    protected override object? GetInputData(AgentContext agentContext)
    {
        return null;
    }
}
