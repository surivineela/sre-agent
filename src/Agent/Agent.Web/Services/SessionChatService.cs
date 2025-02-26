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

        // Get the thread's associated path from AgentManager
        var thread = _agentManager.GetChatThreads().FirstOrDefault(t => t.Id == threadId);
        if (thread != null && !string.IsNullOrEmpty(thread.AgentType))
        {
            // Convert agent type to path
            _currentPath = thread.AgentType.ToLower() == "meta" ? "/" : $"/{thread.AgentType.ToLower()}";
            _logger.LogInformation($"Setting current path to: {_currentPath} based on agent type: {thread.AgentType}");
        }

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
            // Use provided threadId if available
            if (!string.IsNullOrEmpty(threadId))
            {
                _currentThreadId = threadId;
                _logger.LogInformation($"Using provided threadId: {threadId}");

                // Important: Also set the correct path for this thread
                var thread = _agentManager.GetChatThreads().FirstOrDefault(t => t.Id == threadId);
                if (thread != null && !string.IsNullOrEmpty(thread.AgentType))
                {
                    // Convert agent type to path
                    _currentPath = thread.AgentType.ToLower() == "meta" ? "/" : $"/{thread.AgentType.ToLower()}";
                    _logger.LogInformation($"Setting current path to: {_currentPath} based on agent type: {thread.AgentType}");
                }
            }
            else
            {
                _logger.LogInformation($"No threadId provided, using current threadId: {_currentThreadId}");
            }

            _logger.LogInformation($"Processing message for path: {_currentPath}");

            // If we still don't have a threadId, create a new one
            if (_currentThreadId == null)
            {
                _currentThreadId = await CreateThreadAsync(_currentPath, "");
                _logger.LogInformation($"Created new thread: {_currentThreadId}");
            }

            _logger.LogInformation("User > " + message);

            // Make sure we start the chat thread with the CORRECT path
            if (!string.IsNullOrEmpty(_currentThreadId))
            {
                await _agentManager.StartChatThread(_currentPath, _currentThreadId);
            }

            // Create user message
            var userMessage = new ChatMessage
            {
                Message = message,
                IsUser = true,
                Timestamp = DateTime.Now
            };

            // Always add to Meta agent's history 
            await _chatHistoryStorage.AddMessageAsync(_currentThreadId, userMessage, "Meta");

            // Process the message
            var response = await _agentManager.TrackChatThread(_currentThreadId, message);

            _logger.LogInformation("Assistant > " + response.Message);

            // Determine which agent responded
            string respondingAgentType = "Meta"; // Default to Meta

            // Check if we have metadata about which agent responded
            if (_httpContextAccessor?.HttpContext?.Items != null &&
                _httpContextAccessor.HttpContext.Items.ContainsKey("LastRespondingAgent"))
            {
                respondingAgentType = _httpContextAccessor.HttpContext.Items["LastRespondingAgent"].ToString();
                _logger.LogInformation($"Message was handled by {respondingAgentType} agent");
            }

            // Store assistant response with the correct agent type
            var htmlResponse = new ChatMessage
            {
                Message = Markdown.ToHtml(response.Message, _markdownPipeline),
                IsUser = false,
                Timestamp = DateTime.Now
            };

            // Add to Meta agent's history
            await _chatHistoryStorage.AddMessageAsync(_currentThreadId, htmlResponse, "Meta");

            // If a specific sub-agent responded, add to that agent's history too
            if (respondingAgentType != "Meta")
            {
                await _chatHistoryStorage.AddMessageAsync(_currentThreadId, userMessage, respondingAgentType);
                await _chatHistoryStorage.AddMessageAsync(_currentThreadId, htmlResponse, respondingAgentType);
            }

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
        // Get the current agent type based on the current path
        string agentType = "Meta";

        if (!string.IsNullOrEmpty(_currentPath) && _currentPath != "/")
        {
            // Extract agent type from path
            agentType = _currentPath.TrimStart('/');
            _logger.LogInformation($"Getting history for thread {threadId} filtered by agent type: {agentType}");
        }
        else
        {
            _logger.LogInformation($"Getting all history for thread {threadId} (Meta agent view)");
        }

        return await _chatHistoryStorage.GetChatHistoryAsync(threadId, agentType);
    }
}