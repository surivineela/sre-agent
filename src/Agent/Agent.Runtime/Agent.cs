using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Options;
using Agent.Core.Configuration;
using Agent.Core.Helpers;

namespace Agent.Runtime
{
    public class Agent
    {
        private readonly string _name;
        private readonly string _instructions;
        private readonly Kernel _kernel;
        private readonly OpenAISettings _openAISettings;

        public Agent(
            string name,
            string instructions,
            Kernel kernel,
            IOptions<AzureSettings> azureSettings)
        {
            _name = name;
            _instructions = instructions;
            _kernel = kernel;
            _openAISettings = azureSettings.Value.OpenAI;
        }

        public string Name => _name;

        public Kernel Kernel => _kernel;

        public async Task<ChatMessageContent> RunFullTurnAsync(ChatHistory history)
        {
            var agentChatHistory = new ChatHistory();
            agentChatHistory.AddSystemMessage(_instructions);
            agentChatHistory.AddRange(history);
            var originalCount = agentChatHistory.Count;

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

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

            ChatMessageContent chatResult = await chatCompletionService.GetChatMessageContentAsync(
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
    }
}
