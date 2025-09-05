// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Models;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Models;
using Markdig;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Services;


public class DeserializableChatMessageContent
{
    public string? AuthorName { get; set; }
    public AuthorRole Role { get; set; }
    public string? Content { get; set; }
    public string? Source { get; set; }

    public DeserializableChatMessageContent(ChatMessageContent chatMessage)
    {
#pragma warning disable SKEXP0001,SKEXP0101
        AuthorName = chatMessage.AuthorName;
        Role = chatMessage.Role;
        Content = chatMessage.Content;
        Source = chatMessage.Source?.ToString();
#pragma warning restore SKEXP0001,SKEXP0101

    }

    // Parameterless constructor for deserialization, if needed
    public DeserializableChatMessageContent() { }
}

public class ChatProcessingService : IChatService
{
    private readonly IConfiguration _config;
    private readonly AsyncReaderWriterLock _lock = new();
    private readonly ILogger<ChatProcessingService> _logger;
    private readonly IKernelService _kernelService;
    private readonly Kernel _emptyKernel;
    private readonly ISessionMessageService _sessionMessageService;
    private readonly MarkdownPipeline _markdownPipeline;
    private readonly ITeamsClient _teamsClient;
    private Dictionary<string, SessionInformation> _sessionCollection;
    private readonly int backoffPeriodInSeconds = 60;

    public ChatProcessingService(
        IConfiguration config,
        ILogger<ChatProcessingService> logger,
        IKernelService kernelService,
        ITeamsClient teamsClient,
        Kernel kernel,
        ISessionMessageService sessionMessageService)
    {
        _teamsClient = teamsClient;
        _config = config;
        _logger = logger;
        _kernelService = kernelService;
        _sessionCollection = new Dictionary<string, SessionInformation>();
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()           // Disable HTML parsing
            .Build();
        _emptyKernel = kernel;
        _sessionMessageService = sessionMessageService;
    }

    private async Task ResetSessionChatHistory(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            throw new ArgumentException($"sessionId {sessionId} is either empty or invalid");
        }

        if (!_sessionCollection.ContainsKey(sessionId))
        {
            return;
        }

        var sessionChatHistory = _sessionCollection[sessionId];
        var systemMessage = sessionChatHistory.ChatHistory.First().Content ?? string.Empty;
        /*var agentMode = sessionChatHistory.AgentMode;
        var agentInfo = AgentFinder.GetAgentPrompts(agentMode).FirstOrDefault();*/

        //TODO: Dump the old chat history object into JSON file for audit purposes

        using var _ = await _lock.AcquireWriterAsync();
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemMessage);
        _sessionCollection[sessionId].ChatHistory = chatHistory;
        return;
    }

    private bool AgentModeExists(string agentMode)
    {
        return Enum.TryParse<AgentMode>(agentMode, out var mode);
    }

    private ChatHistory CloneChatHistory(ChatHistory originalHistory)
    {
        var clonedHistory = new ChatHistory();
        // Iterate through each message in the original chat history and add it to the cloned history.
        for (int i = 0; i < originalHistory.Count; i++)
        {
            clonedHistory.Add(originalHistory[i]);
        }
        return clonedHistory;
    }

    private async Task<bool> IsAgentDone(SessionInformation sessionInfo)
    {
        if (sessionInfo.AgentMode == AgentMode.None)
        {
            return true;
        }
        var userMessage = new ChatMessageContent()
        {
            Role = AuthorRole.User,
            Content = $@"Take a deep look at all the chat history and determine if the Agent has fulfilled the query and provided an appropriate response. If yes, then respond with 'YES' otherwise respond with 'NO'.
            - If the user is greeting the agent, then the agent should be responding with a greeting and a summary of its capabilities.
            - If the agent is processing an incident, then it should have carried out all the tasks planned unless it needs confirmation.
            - Remember the Agent does not run background tasks. So an answer like 'I will now proceed with doing XYZ' is not acceptable. It should actually carry out the tasks planned and only send response once everything is done (unless it's seeking user confirmation)."
        };
        sessionInfo.ChatHistory.Add(userMessage);
        var _kernel = _kernelService.GetKernelForAgentMode(sessionInfo.AgentMode.ToString()).Clone();
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        var promptExecutionSettings = new AzureOpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
            MaxTokens = 100
        };

        var modelName = chatCompletionService.Attributes["DeploymentName"]?.ToString();
        if (modelName != null && modelName.StartsWith("o"))
        {
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            promptExecutionSettings.ReasoningEffort = OpenAI.Chat.ChatReasoningEffortLevel.Medium;
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }

        ChatMessageContent? chatCompletionResult = null;

        try
        {
            chatCompletionResult = await chatCompletionService.GetChatMessageContentAsync(
            sessionInfo.ChatHistory,
            executionSettings: promptExecutionSettings,
            kernel: _kernel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error running IsAgentDone: {ex.Message}. sessionId: {sessionInfo.SessionId}");
            if ((ex.Message?.Contains("HTTP 429") == true) || (ex.InnerException?.Message?.Contains("HTTP 429") == true))
            {
                string statusMessage = $"[is_agent_done][{DateTime.UtcNow}] OpenAI quota was hit. Backing off for a few seconds to try again.";
                await _kernel.LogInformation(statusMessage, _logger, _teamsClient, _sessionMessageService);
                await Task.Delay(backoffPeriodInSeconds * 1000);
                chatCompletionResult = await chatCompletionService.GetChatMessageContentAsync(
                    sessionInfo.ChatHistory,
                    executionSettings: promptExecutionSettings,
                    kernel: _kernel);
            }
            else
            {
                throw;
            }
        }
        var isAgentDone = false;
        if (chatCompletionResult.Content != null && chatCompletionResult.Content.Contains("YES"))
        {
            isAgentDone = true;
        }
        // Remove the user message that was inserted.
        sessionInfo.ChatHistory.Remove(userMessage);
        return isAgentDone;
    }

    private async Task<ChatMessageContent> RunAgentLoop(SessionInformation sessionInfo, int retryLimit = 2)
    {
        _logger.LogInformation($"ChatProcessingService:RunAgentLoop:Start - sessionId: {sessionInfo.SessionId}, chatHistoryLength: {sessionInfo.ChatHistory.Count}");
        ChatMessageContent? chatCompletionResult = null;
        FunctionChoiceBehaviorOptions options = new() { AllowConcurrentInvocation = false };

        var _kernel = _kernelService.GetKernelForAgentMode(sessionInfo.AgentMode.ToString()).Clone();
        _kernel.Data["sessionId"] = sessionInfo.SessionId;
        _kernel.Data["agentMode"] = sessionInfo.AgentMode.ToString();

        if (sessionInfo.Data != null)
        {
            foreach (var key in sessionInfo.Data.Keys)
            {
                _kernel.Data[key] = sessionInfo.Data[key];
            }
        }

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        var promptExecutionSettings = new AzureOpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: options),
            MaxTokens = 10000
        };

        var modelName = chatCompletionService.Attributes["DeploymentName"]?.ToString();
        if (modelName != null && modelName.StartsWith("o"))
        {
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            promptExecutionSettings.ReasoningEffort = OpenAI.Chat.ChatReasoningEffortLevel.High;
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }

        try
        {

            chatCompletionResult = await chatCompletionService.GetChatMessageContentAsync(
                sessionInfo.ChatHistory,
                executionSettings: promptExecutionSettings,
                kernel: _kernel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error running the chat service: {ex.Message}. sessionId: {sessionInfo.SessionId}");
            if ((ex.Message?.Contains("context_length_exceeded") == true) || (ex.InnerException?.Message?.Contains("context_length_exceeded") == true))
            {
                return new ChatMessageContent()
                {
                    Role = AuthorRole.Assistant,
                    Content = $"An error occurred while processing the request: Model context length exceeded. Please 'clear state' and try again. If this happens again after clearing state, there is too much information in the task that you're trying to process."
                };
            }
            else if ((ex.Message?.Contains("HTTP 429") == true) || (ex.InnerException?.Message?.Contains("HTTP 429") == true))
            {
                string statusMessage = $"[run_agent_loop][{DateTime.UtcNow}] OpenAI quota was hit. Backing off for a few seconds to try again.";
                await _kernel.LogInformation(statusMessage, _logger, _teamsClient, _sessionMessageService);
                await Task.Delay(backoffPeriodInSeconds * 1000);
                chatCompletionResult = await chatCompletionService.GetChatMessageContentAsync(
                    sessionInfo.ChatHistory,
                    executionSettings: promptExecutionSettings,
                    kernel: _kernel);
            }
            else if ((ex.Message?.Contains("500 (Internal Server Error)") == true) || (ex.InnerException?.Message?.Contains("500 (Internal Server Error)") == true))
            {
                string statusMessage = $"[run_agent_loop][{DateTime.UtcNow}] OpenAI internal server error occurred. Backing off for a few seconds to try again.";
                await _kernel.LogInformation(statusMessage, _logger, _teamsClient, _sessionMessageService);
                await Task.Delay(5 * 1000);
                chatCompletionResult = await chatCompletionService.GetChatMessageContentAsync(
                    sessionInfo.ChatHistory,
                    executionSettings: promptExecutionSettings,
                    kernel: _kernel);
            }
            else
            {
                throw;
            }
        }

        sessionInfo.ChatHistory.AddMessage(chatCompletionResult.Role, chatCompletionResult.Content ?? string.Empty);

        _logger.LogInformation($"ChatProcessingService:RunAgentLoop:ChatCompletionResult - {chatCompletionResult.Content}");

        if (retryLimit > 0)
        {
            _logger.LogInformation($"ChatProcessingService:RunAgentLoop - Checking if Agent is done. RetryLimit: {retryLimit}");
            var isAgentDone = await IsAgentDone(sessionInfo);
            _logger.LogInformation($"ChatProcessingService:RunAgentLoop - isAgentDone = {isAgentDone}.");
            if (!isAgentDone)
            {
                sessionInfo.ChatHistory.AddUserMessage("proceed");
                chatCompletionResult = await RunAgentLoop(sessionInfo, retryLimit - 1);
            }
        }

        return chatCompletionResult;
    }

    public async Task<ChatMessage> ProcessMessageAsync(MessageRequestBody message, SessionInformation? sessionInfo = null)
    {
        try
        {
            _logger.LogInformation($"ChatProcessingService:ProcessMessageAsync - {JsonConvert.SerializeObject(message)}");

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message), "MessageRequestBody cannot be null");
            }
            if (string.IsNullOrEmpty(message.Message))
            {
                throw new ArgumentException("Message cannot be empty", nameof(message));
            }
            if (string.IsNullOrEmpty(message.AgentMode))
            {
                throw new ArgumentException("AgentMode cannot be empty", nameof(message));
            }
            if (string.IsNullOrEmpty(message.Sender))
            {
                throw new ArgumentException("Sender cannot be empty", nameof(message));
            }
            if (string.IsNullOrEmpty(message.SessionId))
            {
                throw new ArgumentException("SessionId cannot be empty", nameof(message));
            }


            if (sessionInfo == null && !_sessionCollection.ContainsKey(message.SessionId))
            {
                var foundAgent = AgentModeExists(message.AgentMode);
                if (!foundAgent)
                {
                    throw new ArgumentException($"Agent {message.AgentMode} not found", nameof(message));
                }
                _sessionCollection[message.SessionId] = new SessionInformation(message.SessionId, message.AgentMode);
            }

            sessionInfo = sessionInfo ?? _sessionCollection[message.SessionId];
            if (message.Data?.Any() == true)
            {
                sessionInfo.Data = message.Data;
            }

            if ((message.Message == "clear state" || message.Message == "<p>clear state</p>"))
            {
                _logger.LogInformation("Clearing state");
                await ResetSessionChatHistory(sessionInfo.SessionId);
                return new ChatMessage()
                {
                    Message = "State cleared",
                    Timestamp = DateTime.Now
                };
            }

            if (sessionInfo.AgentLoopRunning)
            {
                //var agentStatusSummary = await GetAgentStatusSummary(sessionInfo);
                return new ChatMessage()
                {
                    Message = $"Agent is currently busy with processing an earlier request in this session.",
                    Timestamp = DateTime.Now
                };
            }

            sessionInfo.AgentLoopRunning = true;
            try
            {
                //Add custom instructions and alert details to the system message if they are present
                if (sessionInfo.ChatHistory.Count == 1 && message.PromptReplacements != null && message.PromptReplacements.Keys.Count() > 0)
                {
                    var systemMessage = sessionInfo.ChatHistory.First().Content ?? string.Empty;
                    foreach (var promptKey in message.PromptReplacements.Keys)
                    {
                        systemMessage = systemMessage.Replace(promptKey, message.PromptReplacements[promptKey]);
                    }
                    sessionInfo.ChatHistory.Clear();
                    sessionInfo.ChatHistory.AddSystemMessage(systemMessage);
                }

                sessionInfo.ChatHistory.AddUserMessage($"User({message.Sender}) > " + message.Message);

                _logger.LogInformation($"User({message.Sender}) > " + message.Message);

                var result = await RunAgentLoop(sessionInfo);

                _logger.LogInformation("Assistant > " + result);
                sessionInfo.ChatHistory.AddMessage(result.Role, result.Content ?? string.Empty);

                string content = result.Content ?? string.Empty;
                var htmlContent = Markdown.ToHtml(content, _markdownPipeline);

                if (_teamsClient.IsEnabled())
                {
                    _logger.LogInformation($"Posting message to Teams: {htmlContent}");

                    var teamsMessage = new TeamsMessage(htmlContent, null)
                    {
                        User = message.Sender,
                        Title = message.Title ?? string.Empty,
                        MessageId = message.SessionId ?? string.Empty,
                    };
                    await _teamsClient.PostMessageOnTeams(message.AgentMode, teamsMessage);
                }

                await _sessionMessageService.GetPublisher(sessionInfo.SessionId).Invoke(content);
                _sessionMessageService.DeleteSession(sessionInfo.SessionId);

                sessionInfo.AgentLoopRunning = false;

                return new ChatMessage()
                {
                    Message = htmlContent,  // Raw markdown
                    Timestamp = DateTime.Now
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message: {ex.Message}. sessionId: {sessionInfo.SessionId}");
                sessionInfo.AgentLoopRunning = false;
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing message: {ex.Message}");
            string errorMessage = "An error occurred while processing the request. Please 'clear state' and try again.";
            if (_teamsClient.IsEnabled())
            {
                _logger.LogInformation($"Posting message to Teams: {errorMessage}");
                var teamsMessage = new TeamsMessage(errorMessage, null);
                await _teamsClient.PostMessageOnTeams(message.AgentMode, teamsMessage);
            }
            return new ChatMessage()
            {
                Message = errorMessage,
                Timestamp = DateTime.Now
            };
        }
    }
}

