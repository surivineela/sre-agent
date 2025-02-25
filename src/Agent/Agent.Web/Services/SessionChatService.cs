namespace Agent.Web.Services;

using System;
using System.Collections.Generic;
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
    private readonly IChatHistoryStorage _chatHistoryStorage;
    private string? _currentThreadId;
    private string _currentPath = "/";

    public SessionChatService(
        ILogger<SessionChatService> logger,
        IAgentManager agentManager,
        IHttpContextAccessor httpContextAccessor,
        IChatHistoryStorage chatHistoryStorage)
    {
        _logger = logger;
        _agentManager = agentManager;
        _httpContextAccessor = httpContextAccessor;
        _chatHistoryStorage = chatHistoryStorage;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();
    }

    public async Task SwitchAgent(string path, string threadId)
    {
        try
        {
            _logger.LogInformation($"SwitchAgent called with path: {path}, threadId: {threadId}");

            // Create new thread if threadId is empty
            if (string.IsNullOrEmpty(threadId))
            {
                threadId = Guid.NewGuid().ToString();
                _logger.LogInformation($"Created new threadId: {threadId}");
            }

            // Normalize path
            path = path.ToLower().Trim('/');
            if (string.IsNullOrEmpty(path))
            {
                path = "";
            }
            _currentPath = "/" + path;
            _currentThreadId = threadId;  // Set the current thread ID

            _logger.LogInformation($"Chat path set to: {_currentPath}");

            await _agentManager.StartChatThread(_currentPath, threadId);

            _logger.LogInformation($"Successfully switched to agent: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error switching to agent path: {path}. Falling back to root agent.");
            _currentPath = "/";

            // Ensure we have a valid threadId even in error case
            if (string.IsNullOrEmpty(_currentThreadId))
            {
                _currentThreadId = Guid.NewGuid().ToString();
            }

            await _agentManager.StartChatThread("/", _currentThreadId);
        }
    }

    public async Task<string> CreateThreadAsync(string path, string threadId)
    {
        try
        {
            // threadId can be empty, in which case a new thread will be created
            if (string.IsNullOrEmpty(threadId))
            {
                threadId = Guid.NewGuid().ToString();
            }
            await _agentManager.StartChatThread(path, threadId);
            _logger.LogInformation($"Created new thread: {threadId} with path: {path}");
            return threadId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating thread with path: {path}. Creating with root agent.");
            await _agentManager.StartChatThread("/", threadId);
            return threadId;
        }
    }

    public async Task<List<ChatThread>> GetThreadsAsync()
{
    var threads = _agentManager.GetChatThreads();
    var chatThreadTasks = threads.Select(async t => new ChatThread
    {
        Id = t.Id,
        Name = t.Name,
        AgentType = t.AgentType ?? "Meta",
        CreatedAt = t.CreatedAt,
        LastMessageAt = await _chatHistoryStorage.GetLastMessageTimestampAsync(t.Id) ?? t.CreatedAt
    });
    
    // Convert the result array to a List
    var results = await Task.WhenAll(chatThreadTasks);
    return results.ToList();
}

    public async Task SetThreadAsync(string threadId)
    {
        if (_currentThreadId == threadId)
        {
            return;
        }
        _logger.LogInformation($"Switching to thread: {threadId} from {_currentThreadId}");
        _currentThreadId = threadId;

        // Start the chat thread with the current path
        await _agentManager.StartChatThread(_currentPath, threadId);
    }

    public async Task<string?> GetCurrentThreadIdAsync()
    {
        return _currentThreadId;
    }

    public async Task<ChatMessage> ProcessMessageAsync(string message, string? threadId)
    {
        try
        {
            _logger.LogInformation($"Processing message for path: {_currentPath}");

            // Use provided threadId if available
            if (!string.IsNullOrEmpty(threadId))
            {
                _currentThreadId = threadId;
                _logger.LogInformation($"Using provided threadId: {threadId}");
            }
            else
            {
                _logger.LogInformation($"No threadId provided, using current threadId: {_currentThreadId}");
            }

            // If we still don't have a threadId, create a new one
            if (_currentThreadId == null)
            {
                _currentThreadId = await CreateThreadAsync(_currentPath, "");
                _logger.LogInformation($"Created new thread: {_currentThreadId}");
            }

            _logger.LogInformation("User > " + message);

            // Ensure we have a valid thread before tracking
            if (!string.IsNullOrEmpty(_currentThreadId))
            {
                await _agentManager.StartChatThread(_currentPath, _currentThreadId);
            }

            var response = await _agentManager.TrackChatThread(_currentThreadId, message);

            _logger.LogInformation("Assistant > " + response.Message);

            // Store user message in history
            var userMessage = new ChatMessage
            {
                Message = message,
                IsUser = true,
                Timestamp = DateTime.Now
            };
            await _chatHistoryStorage.AddMessageAsync(_currentThreadId, userMessage);

            // Store and return assistant response
            var htmlResponse = new ChatMessage
            {
                Message = Markdown.ToHtml(response.Message, _markdownPipeline),
                IsUser = false,
                Timestamp = DateTime.Now
            };
            await _chatHistoryStorage.AddMessageAsync(_currentThreadId, htmlResponse);

            return htmlResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            throw;
        }
    }

    public async Task<List<ChatMessage>> GetChatHistoryAsync(string threadId)
    {
        return await _chatHistoryStorage.GetChatHistoryAsync(threadId);
    }
}