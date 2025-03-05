using System.Runtime.CompilerServices;
using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Agent.Runtime
{
    public class Agent : IAgent
    {
        private const int MAX_FUNCTION_NAME_LENGTH = 64;
        private readonly string _name;
        private readonly string _instructions;
        private readonly Kernel _kernel;
        private readonly OpenAISettings _openAISettings;
        private readonly ILogger<Agent> _logger;
        private readonly IChatCompletionService _chatCompletionService;

        protected IChatClient ChatClient { get; }
        public IList<Microsoft.Extensions.AI.ChatMessage> ChatHistory { get; private set; }
        protected virtual string SystemPrompt => _instructions;

        protected ChatOptions ChatOptionsWithTools => new ChatOptions
        {
            Tools = Tools()
        };

        public Agent(
            string name,
            string instructions,
            Kernel kernel,
            OpenAISettings openAISettings,
            IChatClient chatClient,
            ILogger<Agent> logger)
        {
            try
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _logger.LogInformation("Initializing Agent with name: {Name}", name);

                _name = name ?? throw new ArgumentNullException(nameof(name));
                _instructions = instructions ?? throw new ArgumentNullException(nameof(instructions));
                _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));

                _openAISettings = openAISettings;

                _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

                ChatClient = chatClient
                    .AsBuilder()
                    .UseFunctionInvocation()
                    .Build();

                InitChatHistory();

                _logger.LogInformation("Agent initialization completed successfully");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to initialize Agent");
                throw;
            }
        }

        private void InitChatHistory()
        {
            ChatHistory = [new(ChatRole.System, SystemPrompt)];
        }

        public virtual IList<AITool> Tools()
        {
            return [];
        }

        public virtual async Task<string> Ask(string question, ChatHistory? history)
        {
            await DoWork(question);

            ChatHistory.Add(new(ChatRole.User, $"What was the answer to the following question, if you answered it: {question}"));
            var completion = await ChatClient.GetResponseAsync(ChatHistory, new ChatOptions());
            ChatHistory.Add(new(ChatRole.Assistant, completion.Message.Text));
            return completion.Message.Text;
        }

        public virtual async Task DoWork(string question)
        {
            ChatHistory.Add(new(ChatRole.User, question));
            ChatResponse completion = await ChatClient.GetResponseAsync(ChatHistory, ChatOptionsWithTools);
            ChatHistory.Add(new(ChatRole.Assistant, completion.Message.Text));
        }

        public string Name => _name;

        public Kernel Kernel => _kernel;

        public async Task<ChatMessageContent> RunFullTurnAsync(ChatHistory history)
        {
            try
            {
                _logger.LogInformation("Starting RunFullTurnAsync");

                // Log plugin functions for debugging
                var plugins = _kernel.Plugins;
                _logger.LogInformation($"Found {plugins.Count()} plugins for Agent: {_name}");

                // Update MetaAgentPlugin with current chat history, the instructions shouldn't be added to the history for sub-agents as it's specific to this agent only.
                UpdateSubAgentChatHistoryForMetaAgent(history);


                var agentChatHistory = new ChatHistory();
                agentChatHistory.AddSystemMessage(_instructions);
                agentChatHistory.AddRange(history);
                var originalCount = agentChatHistory.Count;

                var executionSettings = new OpenAIPromptExecutionSettings();

                if (ModelSelectionHelper.IsReasoningModel(_openAISettings.LLMDeploymentName))
                {
                    executionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true);
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    executionSettings.ReasoningEffort = "high";
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                }
                else
                {
                    executionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true);
                }

                ChatMessageContent chatResult = await _chatCompletionService.GetChatMessageContentAsync(
                    agentChatHistory,
                    executionSettings,
                    _kernel);

                history.Add(chatResult);

                return chatResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in RunFullTurnAsync. Exception type: {ex.GetType().Name}, Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner exception details");
                }
                throw;
            }
        }

        public async IAsyncEnumerable<string> StreamResponseAsync(
        string message, ChatHistory history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting StreamResponseAsync for message: {Message}", message);

            // Log plugin functions for debugging
            var plugins = _kernel.Plugins;
            _logger.LogInformation($"Found {plugins.Count()} plugins for Agent: {_name}");

            // Update MetaAgentPlugin with current chat history, the instructions shouldn't be added to the history for sub-agents as it's specific to this agent only.
            UpdateSubAgentChatHistoryForMetaAgent(history);

            // Create a chat history for this interaction, similar to RunFullTurnAsync
            var streamChatHistory = new ChatHistory();
            streamChatHistory.AddSystemMessage(_instructions);
            streamChatHistory.AddRange(history);
            var originalCount = streamChatHistory.Count;

            // Create execution settings, same as in RunFullTurnAsync
            var executionSettings = new OpenAIPromptExecutionSettings();

            if (ModelSelectionHelper.IsReasoningModel(_openAISettings.LLMDeploymentName))
            {
                executionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true);
#pragma warning disable SKEXP0010
                executionSettings.ReasoningEffort = "high";
#pragma warning restore SKEXP0010
            }
            else
            {
                executionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true);
            }

            // Create a StringBuilder to accumulate the full response
            StringBuilder fullResponseBuilder = new StringBuilder();

            // Get streaming response
            var streamingResponse = _chatCompletionService.GetStreamingChatMessageContentsAsync(
                streamChatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            // Process each update
            await foreach (var update in streamingResponse.WithCancellation(cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Content))
                {
                    fullResponseBuilder.Append(update.Content);
                    yield return update.Content;
                }
            }

            // Update chat history
            string fullResponse = fullResponseBuilder.ToString();

            // Log the full response
            Console.WriteLine($"Under agent Complete response ({fullResponse.Length} characters): {fullResponse}");

            // Create a chat message content from the full response
            var chatResult = new ChatMessageContent(AuthorRole.Assistant, fullResponse);
            history.Add(chatResult);
        }

        /// <summary>
        /// Updates the MetaAgentPlugin with the current chat history, should only be used by meta agent.
        /// </summary>
        private void UpdateSubAgentChatHistoryForMetaAgent(ChatHistory history)
        {
            try
            {
                // Get the MetaAgentPlugin instance via dependency injection from the service provider
                var metaPlugin = _kernel.Services.GetService(typeof(MetaAgentPlugin)) as MetaAgentPlugin;
                if (metaPlugin != null)
                {
                    metaPlugin.UpdateChatHistory(history);
                    _logger.LogInformation("Updated MetaAgentPlugin with current chat history");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating MetaAgentPlugin chat history");
            }
        }
    }
}
