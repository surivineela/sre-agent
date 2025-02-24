using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models
{
    public abstract class SubAgent : IAgent
    {
        protected abstract string SystemPrompt { get; }
        protected IChatClient _chatClient { get; }
        public IList<Microsoft.Extensions.AI.ChatMessage> ChatHistory { get; private set; }
        public string Name { get; }
        public Kernel Kernel => null; // TODO: SubAgents need Kernel as well to support full conversation and debugging.

        protected ChatOptions ChatOptionsWithTools => new ChatOptions
        {
            Tools = Tools()
        };

        public SubAgent(string name, IChatClient chatClient)
        {
            Name = name;
            _chatClient = chatClient
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            InitChatHistory();
        }

        private void InitChatHistory()
        {
            ChatHistory = [new(ChatRole.System, SystemPrompt)];
        }

        public abstract IList<AITool> Tools();

        /// <summary>
        /// Try to answer the question
        /// Can implement any of the patterns found here: https://www.anthropic.com/research/building-effective-agents
        /// This default implementation simply gives the agent one chance to use as many tool calls as it wants in a single attempt to answer the question
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public virtual async Task DoWork(string question)
        {
            ChatHistory.Add(new(ChatRole.User, question));
            ChatResponse completion = await _chatClient.GetResponseAsync(ChatHistory, ChatOptionsWithTools);
            ChatHistory.Add(new(ChatRole.Assistant, completion.Message.Text));
        }

        public async Task<string> Ask(string question)
        {
            await DoWork(question);

            // Try to synthesize answer from work
            ChatHistory.Add(new(ChatRole.User, $"What was the answer to the following question, if you answered it: {question}"));
            var completion = await _chatClient.GetResponseAsync(ChatHistory, new ChatOptions());
            ChatHistory.Add(new(ChatRole.Assistant, completion.Message.Text));
            return completion.Message.Text;
        }
    }
}
