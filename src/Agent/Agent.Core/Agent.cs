using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;

namespace Agent.Core
{
    public class Agent(
        string name,
        string instructions,
        Kernel kernel)
    {
        private readonly string _name = name;
        private readonly string _instructions = instructions;
        private readonly Kernel _kernel = kernel;

        public string Name => _name;

        public Kernel Kernel => _kernel;

        public async Task<ChatMessageContent> RunFullTurnAsync(ChatHistory history)
        {
            var agentChatHistory = new ChatHistory();
            agentChatHistory.AddSystemMessage(_instructions);
            agentChatHistory.AddRange(history);
            var originalCount = agentChatHistory.Count;

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            // Enable auto function calling
            OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            ChatMessageContent chatResult = await chatCompletionService.GetChatMessageContentAsync(
                agentChatHistory,
                openAIPromptExecutionSettings,
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
