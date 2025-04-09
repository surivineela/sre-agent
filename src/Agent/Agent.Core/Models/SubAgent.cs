// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Models.Api.v1;
using DurableTask.Core;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;
using System.Text;

namespace Agent.Core.Models
{
    public abstract class SubAgent : IAgent
    {
        public abstract string SystemPrompt { get; protected set; }
        protected IChatClient _chatClient { get; }
        public IList<Microsoft.Extensions.AI.ChatMessage> ChatHistory { get; private set; }
        public string Name { get; }
        public Kernel Kernel => null; // TODO: SubAgents need Kernel as well to support full conversation and debugging.

        protected ChatOptions ChatOptionsWithTools => new ChatOptions
        {
            Tools = Tools()
        };

        public SubAgent(string name, IChatClient chatClient, bool isSkippingInitChatHistory = false)
        {
            Name = name;
            _chatClient = chatClient
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            if (!isSkippingInitChatHistory)
            {
                InitChatHistory();
            }
        }

        private void InitChatHistory()
        {
            ChatHistory = [new(ChatRole.System, SystemPrompt)];
        }

        public void InitChatHistoryFromMessageQueue(Queue<Message> messages)
        {
            foreach (var message in messages)
            {
                if (message.Author.Role == Role.User)
                {
                    ChatHistory.Add(new(ChatRole.User, message.Text));
                }
                else if (message.Author.Role == Role.SREAgent)
                {
                    ChatHistory.Add(new(ChatRole.Assistant, message.Text));
                }
                else if (message.Author.Role == Role.System)
                {
                    ChatHistory.Add(new(ChatRole.System, message.Text));
                }
            }
        }

        public abstract IList<AITool> Tools();

        public abstract Task<IList<Microsoft.Extensions.AI.ChatMessage>> GetStartingMessagesAsync();

        public virtual async Task PrepareAgentForUserInput()
        {
            var startingMessages = await GetStartingMessagesAsync();
            foreach (var message in startingMessages)
            {
                ChatHistory.Add(message);
            }
        }

        /// <summary>
        /// Try to answer the question
        /// Can implement any of the patterns found here: https://www.anthropic.com/research/building-effective-agents
        /// This default implementation simply gives the agent one chance to use as many tool calls as it wants in a single attempt to answer the question
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public virtual async Task<(string Response, bool IsComplete)> DoWork(string question)
        {
            ChatHistory.Add(new(ChatRole.User, question));
            ChatResponse completion = await _chatClient.GetResponseAsync(ChatHistory, ChatOptionsWithTools);
            var agentMessage = completion.Text;
            ChatHistory.Add(new(ChatRole.Assistant, agentMessage));
            return (Response: agentMessage, IsComplete: false);
        }

        public virtual async Task DoWorkWithHistory(string question, ChatHistory externalHistory)
        {
            // First, synchronize our internal chat history with the provided external history
            SynchronizeHistory(externalHistory);
            
            // Then add the question and process it
            ChatHistory.Add(new(ChatRole.User, question));
            ChatResponse completion = await _chatClient.GetResponseAsync(ChatHistory, ChatOptionsWithTools);
            ChatHistory.Add(new(ChatRole.Assistant, completion.GetMessage().Text));
        }

        // To synchronize our chat history with the external history
        protected virtual void SynchronizeHistory(ChatHistory externalHistory)
        {
            // Clear existing chat history except for system message
            var systemMessage = ChatHistory[0]; // Preserve system message
            ChatHistory.Clear();
            ChatHistory.Add(systemMessage);
            
            // Copy relevant messages from external history
            // Skip system messages as we keep our own
            foreach (var message in externalHistory)
            {
                if (message.Role != AuthorRole.System)
                {
                    // Convert from ChatCompletionMessage to Microsoft.Extensions.AI.ChatMessage
                    var role = message.Role == AuthorRole.User 
                        ? ChatRole.User 
                        : ChatRole.Assistant;
                    
                    ChatHistory.Add(new(role, message.Content));
                }
            }
        }

        public async Task<string> Ask(string question, ChatHistory? externalHistory = null)
        {
            if (externalHistory != null)
            {
                await DoWorkWithHistory(question, externalHistory);
            }
            else
            {
                await DoWork(question);
            }

            // Try to synthesize answer from work
            ChatHistory.Add(new(ChatRole.User, $"What was the answer to the following question, if you answered it: {question}"));
            var completion = await _chatClient.GetResponseAsync(ChatHistory, new ChatOptions());

            ChatHistory.Add(new(ChatRole.Assistant, completion.GetMessage().Text));
            return completion.GetMessage().Text;
        }

        public IAsyncEnumerable<string> StreamResponseAsync(
        string message,
        ChatHistory history,
        CancellationToken cancellationToken = default)
        {
            // Now properly use the history parameter
            // First synchronize our history with the external one
            SynchronizeHistory(history);
            
            // Then delegate to the regular stream method
            return StreamResponseAsync(message, cancellationToken);
        }

        public async IAsyncEnumerable<string> StreamResponseAsync(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Add the user's question to chat history
            ChatHistory.Add(new(ChatRole.User, message));
            StringBuilder fullResponseBuilder = new StringBuilder();
            int chunkCounter = 0;

            Console.WriteLine("Under subagent Starting streaming response for message: {0}", message);

            // Get streaming response
            var streamingResponse = _chatClient.GetStreamingResponseAsync(
                ChatHistory,
                ChatOptionsWithTools,
                cancellationToken);

            // Process each update
            await foreach (var update in streamingResponse.WithCancellation(cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    fullResponseBuilder.Append(update.Text);
                    yield return update.Text;
                }
            }

            // Update chat history
            string fullResponse = fullResponseBuilder.ToString();

            // Log the full response
            Console.WriteLine("Under subagent Complete response: {0} characters", fullResponse.Length);
            if (!string.IsNullOrEmpty(fullResponse))
            {
                ChatHistory.Add(new(ChatRole.Assistant, fullResponse));
            }
            else
            {
                Console.WriteLine("Under subagent No response content received from streaming");
            }
        }

        public virtual List<Microsoft.Extensions.AI.ChatMessage> GetUserVisibleChatHistory()
        {
            return ChatHistory.Where(m => m.Role != ChatRole.System).ToList();
        }
    }
}
