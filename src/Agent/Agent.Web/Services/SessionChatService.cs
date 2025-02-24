namespace Agent.Web.Services;

using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Runtime;
using Markdig;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class SessionChatService : IChatService
{
    private readonly ILogger<SessionChatService> _logger;
    private readonly MarkdownPipeline _markdownPipeline;
    private readonly IAgentManager _agentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _currentThreadId;
    private string _currentPath = "/";
    private readonly Dictionary<string, List<ChatMessage>> _threadHistory = new();

    public SessionChatService(
        ILogger<SessionChatService> logger,
        IAgentManager agentManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _agentManager = agentManager;
        _httpContextAccessor = httpContextAccessor;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();
    }

    public async Task SwitchAgent(string path, string threadId)
    {
        // Normalize path
        path = path.ToLower().Trim('/');
        if (string.IsNullOrEmpty(path))
        {
            path = "";
        }
        _currentPath = "/" + path;
        _logger.LogInformation($"Chat path set to: {_currentPath}");
        await _agentManager.StartChatThread(_currentPath, threadId);
    }

    public async Task<string> CreateThreadAsync(string path, string threadId)
    {
        // threadId can be empty, in which case a new thread will be created
        if (string.IsNullOrEmpty(threadId))
        {
            threadId = Guid.NewGuid().ToString();
        }
        await _agentManager.StartChatThread(path, threadId);
        _logger.LogInformation($"Created new thread: {threadId} with path: {path}");
        _threadHistory[threadId] = new List<ChatMessage>();
        return threadId;
    }

    public async Task<List<ChatThread>> GetThreadsAsync()
    {
        var threads = await _agentManager.GetChatThreads();
        // print the threads
        _logger.LogInformation("Available threads:");
        foreach (var thread in threads)
        {
            _logger.LogInformation($"Thread: {thread.Id} - {thread.Name}");
        }
        return threads.Select(t => new ChatThread
        {
            Id = t.Id,
            Name = t.Name,
            CreatedAt = t.CreatedAt
        }).ToList();
    }

    public async Task SetThreadAsync(string threadId)
    {
        if (_currentThreadId == threadId)
        {
            return;
        }
        _logger.LogInformation($"Switching to thread: {threadId} from {_currentThreadId}");
        _currentThreadId = threadId;
        if (!_threadHistory.ContainsKey(threadId))
        {
            _threadHistory[threadId] = new List<ChatMessage>();
        }
    }

    public async Task<string?> GetCurrentThreadIdAsync()
    {
        return _currentThreadId;
    }

    public async Task<ChatMessage> ProcessMessageAsync(string message)
    {
        try
        {
            _logger.LogInformation($"Processing message for path: {_currentPath}");

            if (_currentThreadId == null)
            {
                _currentThreadId = await CreateThreadAsync(_currentPath, "");
                _logger.LogInformation($"Created new thread: {_currentThreadId}");
            }

            _logger.LogInformation("User > " + message);

            var response = await _agentManager.TrackChatThread(_currentThreadId, message);

            _logger.LogInformation("Assistant > " + response.Message);

            response.Message = Markdown.ToHtml(response.Message, _markdownPipeline);

            if (_threadHistory.ContainsKey(_currentThreadId))
            {
                _threadHistory[_currentThreadId].Add(new ChatMessage
                {
                    Message = message,
                    IsUser = true,
                    Timestamp = DateTime.Now
                });
                _threadHistory[_currentThreadId].Add(response);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            throw;
        }
    }
}
