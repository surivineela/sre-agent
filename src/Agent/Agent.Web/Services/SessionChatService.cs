namespace Agent.Web.Services;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;
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
    private string? _currentChatId;
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

    public async Task SwitchAgent(string path, string chatId)
    {
        try
        {
            _logger.LogInformation($"SwitchAgent called with path: {path}, chatId: {chatId}");

            // Create new thread if chatId is empty
            if (string.IsNullOrEmpty(chatId))
            {
                chatId = Guid.NewGuid().ToString();
                _logger.LogInformation($"Created new chatId: {chatId}");
            }

            // Normalize path
            path = path.ToLower().Trim('/');
            if (string.IsNullOrEmpty(path))
            {
                path = "";
            }
            _currentPath = "/" + path;
            _currentChatId = chatId;  // Set the current thread ID

            _logger.LogInformation($"Chat path set to: {_currentPath}");

            await _agentManager.StartChatThread(_currentPath, chatId);

            _logger.LogInformation($"Successfully switched to agent: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error switching to agent path: {path}. Falling back to root agent.");
            _currentPath = "/";

            // Ensure we have a valid chatId even in error case
            if (string.IsNullOrEmpty(_currentChatId))
            {
                _currentChatId = Guid.NewGuid().ToString();
            }

            await _agentManager.StartChatThread("/", _currentChatId);
        }
    }

    public async Task<string> StartThreadAsync(string path, string chatId)
    {
        try
        {
            // chatId can be empty, in which case a new thread will be created
            if (string.IsNullOrEmpty(chatId))
            {
                chatId = Guid.NewGuid().ToString();
            }
            await _agentManager.StartChatThread(path, chatId);
            _logger.LogInformation($"Created new thread: {chatId} with path: {path}");
            return chatId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating thread with path: {path}. Creating with root agent.");
            await _agentManager.StartChatThread("/", chatId);
            return chatId;
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

    public async Task SetThreadAsync(string chatId)
    {
        if (_currentChatId == chatId)
        {
            return;
        }
        _logger.LogInformation($"Switching to thread: {chatId} from {_currentChatId}");
        _currentChatId = chatId;

        // Get the thread's associated path from AgentManager
        var thread = _agentManager.GetChatThreads().FirstOrDefault(t => t.Id == chatId);
        if (thread != null && !string.IsNullOrEmpty(thread.AgentType))
        {
            // Convert agent type to path
            _currentPath = thread.AgentType.ToLower() == "meta" ? "/" : $"/{thread.AgentType.ToLower()}";
            _logger.LogInformation($"Setting current path to: {_currentPath} based on agent type: {thread.AgentType}");
        }

        // Start the chat thread with the current path
        await _agentManager.StartChatThread(_currentPath, chatId);
    }

    public async Task<string?> GetCurrentChatIdAsync()
    {
        return _currentChatId;
    }

    public async Task<ChatMessage> ProcessMessageAsync(string message, string? chatId)
    {
        try
        {
            // Use provided chatId if available
            if (!string.IsNullOrEmpty(chatId))
            {
                _currentChatId = chatId;
                _logger.LogInformation($"Using provided chatId: {chatId}");

                // Important: Also set the correct path for this thread
                var thread = _agentManager.GetChatThreads().FirstOrDefault(t => t.Id == chatId);
                if (thread != null && !string.IsNullOrEmpty(thread.AgentType))
                {
                    // Convert agent type to path
                    _currentPath = thread.AgentType.ToLower() == "meta" ? "/" : $"/{thread.AgentType.ToLower()}";
                    _logger.LogInformation($"Setting current path to: {_currentPath} based on agent type: {thread.AgentType}");
                }
            }
            else
            {
                _logger.LogInformation($"No chatId provided, using current chatId: {_currentChatId}");
            }

            _logger.LogInformation($"Processing message for path: {_currentPath}");

            // StartThreadAsync will create new chatId if not exist
            _currentChatId = await StartThreadAsync(_currentPath, _currentChatId ?? "");

            _logger.LogInformation("User > " + message);

            // Create user message
            var userMessage = new ChatMessage
            {
                Message = message,
                IsUser = true,
                Timestamp = DateTime.Now
            };

            // Always add to Meta agent's history 
            await _chatHistoryStorage.AddMessageAsync(_currentChatId, userMessage, "Meta");

            // Process the message
            var response = await _agentManager.TrackChatThread(_currentChatId, message);

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
            await _chatHistoryStorage.AddMessageAsync(_currentChatId, htmlResponse, "Meta");

            // If a specific sub-agent responded, add to that agent's history too
            if (respondingAgentType != "Meta")
            {
                await _chatHistoryStorage.AddMessageAsync(_currentChatId, userMessage, respondingAgentType);
                await _chatHistoryStorage.AddMessageAsync(_currentChatId, htmlResponse, respondingAgentType);
            }

            return htmlResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            throw;
        }
    }

    public async IAsyncEnumerable<string> ProcessMessageStreamAsync(
    string message,
    string? chatId,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Thread and path handling
        if (!string.IsNullOrEmpty(chatId))
        {
            _currentChatId = chatId;
            _logger.LogInformation($"Using provided chatId: {chatId}");

            // Set the correct path for this thread
            var thread = _agentManager.GetChatThreads().FirstOrDefault(t => t.Id == chatId);
            if (thread != null && !string.IsNullOrEmpty(thread.AgentType))
            {
                // Convert agent type to path
                _currentPath = thread.AgentType.ToLower() == "meta" ? "/" : $"/{thread.AgentType.ToLower()}";
                _logger.LogInformation($"Setting current path to: {_currentPath} based on agent type: {thread.AgentType}");
            }
        }
        else
        {
            _logger.LogInformation($"No chatId provided, using current chatId: {_currentChatId}");
        }

        _logger.LogInformation($"Processing message for path: {_currentPath}");

        // StartThreadAsync will create new chatId if not exist
        _currentChatId = await StartThreadAsync(_currentPath, _currentChatId ?? "");

        _logger.LogInformation("User > " + message);

        // Record user message in history
        var userMessage = new ChatMessage
        {
            Message = message,
            IsUser = true,
            Timestamp = DateTime.Now
        };

        // Always add to Meta agent's history 
        await _chatHistoryStorage.AddMessageAsync(_currentChatId, userMessage, "Meta");

        // Prepare for tracking which agent responds
        string respondingAgentType = "Meta";

        // Use a StringBuilder to accumulate the complete response
        StringBuilder responseBuilder = new StringBuilder();

        // Stream each chunk without markdown processing
        await foreach (var chunk in _agentManager.StreamChatThread(_currentChatId, message, cancellationToken))
        {
            // Add to complete response
            responseBuilder.Append(chunk);

            // Stream chunk without markdown processing
            yield return chunk;
        }

        // After streaming is complete, check which agent responded
        if (_httpContextAccessor?.HttpContext?.Items != null &&
            _httpContextAccessor.HttpContext.Items.ContainsKey("LastRespondingAgent"))
        {
            var respondingAgent = _httpContextAccessor.HttpContext.Items["LastRespondingAgent"]?.ToString();

            // Fix: Ensure we don't get an empty string - print what we're getting for debugging
            _logger.LogInformation($"Raw LastRespondingAgent value: '{respondingAgent}'");

            if (!string.IsNullOrEmpty(respondingAgent))
            {
                respondingAgentType = respondingAgent;
                _logger.LogInformation($"Message was handled by {respondingAgentType} agent");
            }
        }
        else
        {
            _logger.LogWarning("LastRespondingAgent not found in HttpContext.Items");
        }

        // Convert the full response to markdown for storage
        string fullResponse = responseBuilder.ToString();
        string htmlResponse = Markdown.ToHtml(fullResponse, _markdownPipeline);

        // Save the complete response
        var completeResponse = new ChatMessage
        {
            Message = htmlResponse,  // Store as HTML in history
            IsUser = false,
            Timestamp = DateTime.Now
        };

        // Add to Meta agent's history
        await _chatHistoryStorage.AddMessageAsync(_currentChatId, completeResponse, "Meta");

        // If a specific sub-agent responded, add to that agent's history too
        if (respondingAgentType != "Meta")
        {
            _logger.LogInformation($"Adding message to {respondingAgentType}'s history as well");
            await _chatHistoryStorage.AddMessageAsync(_currentChatId, userMessage, respondingAgentType);
            await _chatHistoryStorage.AddMessageAsync(_currentChatId, completeResponse, respondingAgentType);
        }
    }

    public async Task<List<ChatMessage>> GetChatHistoryAsync(string chatId)
    {
        // Get the current agent type based on the current path
        string agentType = "Meta";

        if (!string.IsNullOrEmpty(_currentPath) && _currentPath != "/")
        {
            // Extract agent type from path
            agentType = _currentPath.TrimStart('/');
            _logger.LogInformation($"Getting history for thread {chatId} filtered by agent type: {agentType}");
        }
        else
        {
            _logger.LogInformation($"Getting all history for thread {chatId} (Meta agent view)");
        }

        return await _chatHistoryStorage.GetChatHistoryAsync(chatId, agentType);
    }
}