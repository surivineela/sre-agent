using System.Reflection;
using System.Threading.Channels;
using Agent.Core.Attributes;
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
    private Agent<AgentContext> _currentAgent;
    private AgentContext _context;
    private readonly IThreadRepository _threadRepository;

    private readonly List<ChatMessage> _chatHistory;
    private readonly Channel<ChatMessage> _msgCh;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public ReasoningLoop(ILogger<ReasoningLoop> logger,
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IAgentOutboundCommunicationService outboundCommunicationService,
        Agent<AgentContext> startingAgent,
        IThreadRepository threadRepository,
        AgentContext context)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _chatClient = chatClient;
        _outboundCommunicationService = outboundCommunicationService;
        _currentAgent = startingAgent;
        _chatHistory = new List<ChatMessage>();
        _msgCh = Channel.CreateUnbounded<ChatMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = true
        });
        _threadRepository = threadRepository;
        _context = context;
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

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!await _semaphore.WaitAsync(0))
        {
            return;
        }

        while (_msgCh.Reader.TryRead(out var msg))
        {
            try
            {
                _logger.LogInternalInformation("Received new message. Running reasoning loop...");
                _chatHistory.Add(msg);

                // The reasoning loop starts here
                while (true)
                {
                    var output = await Runner.RunAsync(
                        startingAgent: _currentAgent,
                        input: _chatHistory,
                        config: new RunConfig
                        {
                            ChatClient = _chatClient,
                            LoggerFactory = _loggerFactory
                        },
                        context: _context,
                        cancellationToken: cancellationToken
                    );
                    _chatHistory.AddRange(output.NewItems);
                    _currentAgent = output.LastAgent;

                    // Check if there are any manual tool calls (Approval)
                    if (output.ManualToolCalls != null && output.ManualToolCalls.Count > 0)
                    {
                        foreach (var toolCall in output.ManualToolCalls)
                        {
                            var checkResult = await CheckApproval(toolCall);
                            if (checkResult.ApprovalStatus == ToolApprovalStatus.Pending)
                            {
                                // if approval is pending, stop the loop and wait for approval
                                break;
                            }
                            else if (checkResult.ApprovalStatus == ToolApprovalStatus.NotRequired || checkResult.ApprovalStatus == ToolApprovalStatus.Approved)
                            {
                                var functionResult = await toolCall.Tool!.InvokeAsync(toolCall.FunctionCall.Arguments);
                                var result = new FunctionResultContent(toolCall.FunctionCall.CallId, functionResult);
                                _chatHistory.Add(new ChatMessage(ChatRole.Tool, [result]));
                            }
                            else  // ToolApprovalStatus.Denied
                            {
                                var result = new FunctionResultContent(toolCall.FunctionCall.CallId, "denied");
                                _chatHistory.Add(new ChatMessage(ChatRole.Tool, [result]));
                                var denyMsg = new ChatMessage(ChatRole.Assistant, "The approval request of this action got denied.");
                                _chatHistory.Add(denyMsg);
                                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context.ThreadId, string.Empty, denyMsg);
                                break;
                            }
                        }
                    }
                    else
                    {
                        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context.ThreadId, string.Empty,
                            new ChatMessage(ChatRole.Assistant, output.Output?.ToString()));
                        break; // Exit the loop if there are no manual tool calls
                    }
                }

                _logger.LogInternalInformation("Responded to user");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
            }
        }

        _semaphore.Release();
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
                    AgentContextId: null,
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
}
