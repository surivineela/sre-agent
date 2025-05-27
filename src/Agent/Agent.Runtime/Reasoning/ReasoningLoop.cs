using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Agent.Core.Attributes;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Helpers;
using Agent.Runtime.SubAgents.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

public class ReasoningLoop
{
    private readonly ILogger<ReasoningLoop> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IChatClient _chatClient;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private AgentContext _context;
    private readonly IThreadRepository _threadRepository;
    private readonly Channel<ChatMessage> _msgCh;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly IToolFactory _toolFactory;

    private List<ChatMessage>? _chatHistory;
    private Agent<AgentContext> _currentAgent;

    public ReasoningLoop(ILogger<ReasoningLoop> logger,
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IAgentOutboundCommunicationService outboundCommunicationService,
        Agent<AgentContext> startingAgent,
        IThreadRepository threadRepository,
        AgentContext context,
        IToolFactory toolFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _chatClient = chatClient;
        _outboundCommunicationService = outboundCommunicationService;
        _msgCh = Channel.CreateUnbounded<ChatMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = true
        });
        _threadRepository = threadRepository;
        _context = context;
        _toolFactory = toolFactory;
        _currentAgent = startingAgent;
    }

    public async Task AppendNewMessage(ChatMessage msg, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new message");
            await _msgCh.Writer.WriteAsync(msg, cancellationToken);

            _ = Task.Run(async () => await RunAsync(cancellationToken), cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Channel is closed.");
        }
    }

    public async Task LoadChatHistoryAsync()
    {
        if (_chatHistory != null)
        {
            return;
        }

        var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
        if (agentChatHistory == null)
        {
            // should never happen
            _chatHistory = new List<ChatMessage>();
        }

        var reasoningMessages = await agentChatHistory!.GetReasoningMessagesAsync(_threadRepository);
        _chatHistory = reasoningMessages.GetChatMessages();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!await _semaphore.WaitAsync(0))
        {
            return;
        }

        AgentChatHistory agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);

        // TODO: handle imcompleted function calls before starting new reasoning loop
        // This may happen if the agent restart/crash in the middle
        while (_msgCh.Reader.TryRead(out var msg))
        {
            try
            {
                _logger.LogInternalInformation("Received new message. Running reasoning loop...");

                var shouldStop = await ProcessNewApproval(agentChatHistory, cancellationToken);
                if (shouldStop)
                {
                    return;
                }

                await PersistReasoningMessage(agentChatHistory, msg);

                // The reasoning loop starts here
                while (true)
                {
                    var output = await Runner.RunAsync(
                        startingAgent: _currentAgent,
                        input: _chatHistory!,
                        config: new RunConfig
                        {
                            ChatClient = _chatClient,
                            LoggerFactory = _loggerFactory
                        },
                        context: _context,
                        cancellationToken: cancellationToken
                    );

                    await PersistReasoningMessage(agentChatHistory, output.NewItems);
                    _currentAgent = output.LastAgent;

                    _context = _context with { CurrentAgent = _currentAgent.Name };
                    _context = await _threadRepository.UpdateAgentContextAsync(_context);

                    // Check if there are any manual tool calls
                    if (output.ManualToolCalls != null && output.ManualToolCalls.Count > 0)
                    {
                        var toolCall = output.ManualToolCalls.Single(); // Should only be one tool call at a time
                        var checkResult = await CheckApproval(toolCall);
                        if (checkResult.ApprovalStatus == ToolApprovalStatus.NotRequired)
                        {
                            try
                            {
                                var functionResult = await toolCall.Tool.InvokeAsync(toolCall.FunctionCall.Arguments);
                                var result = new FunctionResultContent(toolCall.FunctionCall.CallId, functionResult);
                                var functionCallMessage = new ChatMessage(ChatRole.Tool, [result]);
                                await PersistReasoningMessage(agentChatHistory, functionCallMessage);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInternalError(ex, "Error while invoking tool: {ToolName}", toolCall.Tool.Name);
                                var errorMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(toolCall.FunctionCall.CallId, $"Internal error")]);
                                await PersistReasoningMessage(agentChatHistory, errorMessage);
                            }
                        }
                        else
                        {
                            // if approval is required, stop the loop and wait for approval
                            break;
                        }
                    }
                    else
                    {
                        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context.ThreadId, string.Empty,
                            new ChatMessage(ChatRole.Assistant, output.Output?.ToString()));
                        break; // Exit the loop if there are no manual tool calls
                    }
                }

                _logger.LogInternalInformation("Reasoning loop completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
            }
        }

        _semaphore.Release();
    }

    private async Task<bool> ProcessNewApproval(AgentChatHistory agentChatHistory, CancellationToken cancellationToken)
    {
        var lastMessage = _chatHistory?.LastOrDefault()?.Contents?.First();
        // if lastMessage is a tool call, we need to invoke the tool first
        if (lastMessage != null && lastMessage is FunctionCallContent functionCall)
        {
            var approvalTitle = ApprovalHelper.GenerateUniqueApprovalTitle(
                _context.ThreadId.ToString(),
                _context.AssignedInstanceId ?? string.Empty,
                functionCall.Name,
                functionCall.Arguments ?? new Dictionary<string, object?>());

            var approval = await _threadRepository.GetApprovalAsync(_context.ThreadId, approvalTitle);
            if (approval == null || approval.Status == ApprovalDecision.Approved)
            {
                try
                {
                    var aiTool = _currentAgent.ManualTools.Find(aiTool => aiTool.Name == functionCall!.Name);
                    var functionResult = await aiTool!.InvokeAsync(functionCall.Arguments, cancellationToken);
                    var result = new FunctionResultContent(functionCall.CallId, functionResult);
                    var functionCallMessage = new ChatMessage(ChatRole.Tool, [result]);
                    await PersistReasoningMessage(agentChatHistory, functionCallMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error while invoking tool: {ToolName}", functionCall.Name);
                    var errorMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(functionCall.CallId, $"Internal error")]);
                    await PersistReasoningMessage(agentChatHistory, errorMessage);
                }
            }
            else if (approval.Status == ApprovalDecision.Rejected)
            {
                var result = new FunctionResultContent(functionCall.CallId, "rejected");
                var functionCallMessage = new ChatMessage(ChatRole.Tool, [result]);
                await PersistReasoningMessage(agentChatHistory, functionCallMessage);
            }
            else  // Pending
            {
                // If there is any pending approvals, we should wait for them to be resolved before continuing
                _logger.LogInternalInformation("There are pending approvals. Waiting for them to be resolved before continuing.");
                return true;
            }
        }

        return false;
    }

    private async Task<CheckApprovalActivityOutput> CheckApproval(ManualToolCall toolCall)
    {
        try
        {
            if (toolCall.Tool == null)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            // Check if requiers approval
            var attribute = toolCall.Tool.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();
            if (attribute == null)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            var approvalTitle = ApprovalHelper.GenerateUniqueApprovalTitle(
                _context.ThreadId.ToString(),
                _context.AssignedInstanceId ?? string.Empty,
                toolCall.FunctionCall.Name,
                toolCall.FunctionCall.Arguments ?? new Dictionary<string, object?>());

            var approval = await _threadRepository.GetApprovalAsync(_context.ThreadId, approvalTitle);

            if (approval == null ||
                (approval.Status == ApprovalDecision.Approved && string.IsNullOrEmpty(approval.OboToken) && attribute != null && attribute.UseOboToken))
            {
                var description = attribute.DisplayMessage ?? string.Empty;

                // Create a new approval document
                var newApproval = new Approval(
                    Id: Guid.NewGuid(),
                    ThreadId: _context.ThreadId.ToString(),
                    Title: approvalTitle,
                    Description: description,
                    Status: ApprovalDecision.Pending,
                    CreatedTimestamp: DateTime.UtcNow,
                    DecisionTimestamp: null,
                    OrchestrationId: null,
                    AgentContextId: _context.Id,
                    DecisionUser: null,
                    OboToken: null);

                await _threadRepository.CreateApprovalAsync(newApproval);
                await _outboundCommunicationService.AppendAgentApprovalMessage(
                    _context.ThreadId,
                    newApproval);

                _logger.LogInternalInformation("Created new approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status ToolApprovalStatus.Pending", newApproval.Id, _context.ThreadId, newApproval.Title);

                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = newApproval.Id,
                    ApprovalStatus = ToolApprovalStatus.Pending,
                };
            }
            else
            {
                _logger.LogInternalInformation("Found existing approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status {Status}", approval.Id, _context.ThreadId, approval.Title, approval.Status);
                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = approval.Id,
                    ApprovalStatus = ApprovalDocument.ToToolApprovalStatus(approval.Status),
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error while checking approval: {Message}", ex.Message);
            return new CheckApprovalActivityOutput()
            {
                ApprovalStatus = ToolApprovalStatus.Pending,
            };
        }
    }

    private AIFunction GetAIFunctionWithThreadId(string functionName, Guid threadId)
    {
        return _toolFactory.FindAIFunction(functionName, threadId);
    }

    private async Task PersistReasoningMessage(AgentChatHistory agentChatHistory, ChatMessage chatMessage)
    {
        _chatHistory!.Add(chatMessage);
        var reasoningMessage = chatMessage.GetReasoningMessage(_context.Id);
        await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);

        await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage);
    }
    
    private async Task PersistReasoningMessage(AgentChatHistory agentChatHistory, IEnumerable<ChatMessage> chatMessage)
    {
        _chatHistory!.AddRange(chatMessage);

        var reasoningMessages = chatMessage.Select(msg => msg.GetReasoningMessage(_context.Id));
        foreach (var reasoningMessage in reasoningMessages)
        {
            await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);
        }   
        
        await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessages);
    }
}
