using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Options;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Agent.Core.Models;

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
            IOptions<AzureSettings> azureSettings,
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

                if (azureSettings?.Value?.OpenAI == null)
                {
                    _logger.LogError("Azure settings or OpenAI configuration is null");
                    throw new ArgumentNullException(nameof(azureSettings));
                }
                _openAISettings = azureSettings.Value.OpenAI;

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

        public virtual async Task<string> Ask(string question)
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


                var agentChatHistory = new ChatHistory();
                agentChatHistory.AddSystemMessage(_instructions);
                agentChatHistory.AddRange(history);
                var originalCount = agentChatHistory.Count;

                var executionSettings = new OpenAIPromptExecutionSettings();

                if (ModelSelectionHelper.IsReasoningModel(_openAISettings.DeploymentName))
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

                for (int i = originalCount - 1; i < agentChatHistory.Count; i++)
                {
                    history.Add(agentChatHistory[i]);
                }

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
    }
}
