// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Helpers;
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
    private readonly IToolsRepository _toolsRepository;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly ILoggerFactory _loggerFactory;

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
        _chatClient = chatClient;
        _threadRepository = threadRepository;
        _toolsRepository = toolsRepository;
        _outboundCommunicationService = outboundCommunicationService;
        _loggerFactory = loggerFactory;

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

        _tools.AddRange(ResolveSubAgentTools());
    }

    private List<AITool> ResolveSubAgentTools()
    {
        List<AITool> tools = [];

        var aiFunctions = _toolsRepository.GetAllTools(TDefinition.ToolSignatures).Select(_toolsRepository.FindAiFunction);

        foreach (var aiFunction in aiFunctions)
        {
            var underlyingMethod = aiFunction.ToolFunction.UnderlyingMethod;

            if (underlyingMethod == null)
            {
                continue;
            }

            if (ApprovalHelper.ToolRequiresApproval(aiFunction))
            {
                // the interceptor wraps the AI function call to check for approval
                var approvalInterceptor = new ApprovalInterceptor(
                    _context,
                    aiFunction.ToolFunction,
                    _threadRepository,
                    _outboundCommunicationService,
                    _loggerFactory.CreateLogger<ApprovalInterceptor>());

                tools.Add(approvalInterceptor);
            }
            else
            {
                tools.Add(aiFunction.ToolFunction);
            }
        }

        return tools;
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
            var systemPrompt = GetSystemPromptMessage();
            var systemPromptReasoningMessage = systemPrompt.GetReasoningMessage(_context.Id);
            await _threadRepository.CreateReasoningMessageAsync(systemPromptReasoningMessage);
            await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, systemPromptReasoningMessage);
            chatMessages.Add(systemPrompt);
        }

        // get model response
        var messagesToSend = _toolsRepository.GetMCPServerInstructions().Concat(chatMessages);
        var chatResponse = await _chatClient.GetResponseAsync(messagesToSend, ChatOptionsWithTools);

        // persist responses
        var responseMessages = chatResponse.GetReasoningMessages(_context.Id);

        foreach (var reasoningMessage in responseMessages)
        {
            await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);
        }

        // update chat history
        await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, responseMessages);

        // post last agent message to the thread
        var lastMessage = chatResponse.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant);
        var lastMessageText = lastMessage?.Contents.OfType<TextContent>().FirstOrDefault();

        if (lastMessage != null && lastMessageText != null)
        {
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, lastMessage);
        }
    }

    protected virtual TInput? GetInputData()
    {
        TInput? inputData = default;

        if (!string.IsNullOrEmpty(_context.InputDataSerialized))
        {
            try
            {
                inputData = JsonSerializer.Deserialize<TInput>(_context.InputDataSerialized);
            }
            catch
            {
                // swallow exception
            }
        }

        return inputData;
    }

    private ChatMessage GetSystemPromptMessage()
    {
        // Load common prompt stubs
        var controlFlowPromptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "V2", "PromptStubs", "ControlFlowPromptStub.txt");
        var controlFlowPrompt = File.ReadAllText(controlFlowPromptPath);
        var communicationGuidelines = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "V2", "PromptStubs", "CommunicationGuidelinesPromptStub.txt");
        var communicationGuidelinesPrompt = File.ReadAllText(communicationGuidelines);
        var approvalGuidelines = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "V2", "PromptStubs", "ApprovalsPromptStub.txt");
        var approvalGuidelinesPrompt = File.ReadAllText(approvalGuidelines);

        var systemPrompt = new StringBuilder();
        systemPrompt.AppendLine(controlFlowPrompt);
        systemPrompt.AppendLine(communicationGuidelinesPrompt);
        systemPrompt.AppendLine(approvalGuidelinesPrompt);

        // Add customized system prompt from subagent definition
        systemPrompt.AppendLine(TDefinition.GetSystemPrompt(GetInputData()));

        return new ChatMessage(ChatRole.System, systemPrompt.ToString());
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
    protected override object? GetInputData()
    {
        return null;
    }
}
