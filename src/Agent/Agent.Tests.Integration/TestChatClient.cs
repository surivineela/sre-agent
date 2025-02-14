using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace Agent.Tests.Integration
{
    internal class TestChatClient
    {
        internal IList<ChatMessage> ChatHistory { get; }
        internal ChatOptions ChatOptions { get; }
        internal IChatClient Client { get; }
        internal ITestOutputHelper _output { get; }

        internal TestChatClient(IChatClient client, ChatOptions chatOptions, ITestOutputHelper output)
        {
            ChatHistory = [];
            ChatOptions = chatOptions;
            Client = client;
            _output = output;
        }

        public async Task<ChatCompletion> CompleteAsync(string message)
        {
            _output.WriteLine($"Sending message: {message}");

            var userMessage = new ChatMessage(ChatRole.User, message);
            ChatHistory.Add(userMessage);
            ChatCompletion completion = await Client.CompleteAsync(ChatHistory, ChatOptions);
            var assistantMessage = new ChatMessage(ChatRole.Assistant, completion.Message.Text);
            ChatHistory.Add(assistantMessage);
            return completion;
        }

        public async Task<bool> MatchesNaturalLanguagePrompt(string expected)
        {
            IList<ChatMessage> tmpChatHistory = ChatHistory.Where(m => m.Role == ChatRole.Assistant || m.Role == ChatRole.Tool).ToList();

            tmpChatHistory.Add(new(ChatRole.User, $@"You are part of an end to end unit testing framework.
Your job is simply to respond with `true` or `false` depending on if the logs from this chat history match the expected text.
The text doesn't have to match exactly, but it needs to be close enough that a human would say it's an acceptible response for what we're trying to accomplish.

Expected: {expected}"
            ));
            ChatCompletion completion = await Client.CompleteAsync(tmpChatHistory);

            bool succeeded = bool.TryParse(completion.Message.Text, out var result);
            if (!succeeded)
            {
                throw new Exception($"Natural language test failed to parse the result. Response was: {completion}");
            }

            _output.WriteLine($@"LLM ruled that the output {(result ? "DID" : "DID NOT")} match the following query: ""{expected}""");
            return result;
        }
    }
}
