using Agent.Logging;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Framework;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;

public class ReasoningLoop
{
    private readonly ILogger<ReasoningLoop> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IChatClient _chatClient;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private Agent<AgentContext> _currentAgent;
    private AgentContext _context;

    private readonly List<ChatMessage> _chatHistory;
    private readonly Channel<ChatMessage> _msgCh;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public ReasoningLoop(ILogger<ReasoningLoop> logger,
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IAgentOutboundCommunicationService outboundCommunicationService,
        Agent<AgentContext> startingAgent,
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
                var result = await Runner.RunAsync(_currentAgent, _chatHistory, new RunConfig
                {
                    LoggerFactory = _loggerFactory,
                    ChatClient = _chatClient,
                }, context: _context, cancellationToken: cancellationToken);

                _chatHistory.AddRange(result.NewItems);
                _currentAgent = result.LastAgent;

                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context.ThreadId, string.Empty,
                    new ChatMessage(ChatRole.Assistant, result.Output?.ToString()));

                _logger.LogInternalInformation("Responded to user");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
            }
        }

        _semaphore.Release();
    }
}
