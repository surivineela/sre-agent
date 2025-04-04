// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

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

        internal TestChatClient(IChatClient client, ChatOptions chatOptions, string systemPrompt, ITestOutputHelper output)
        {
            ChatHistory = [new ChatMessage(ChatRole.System, systemPrompt)];
            ChatOptions = chatOptions;
            Client = client;
            _output = output;
        }

        public async Task<ChatResponse> CompleteAsync(string message)
        {
            _output.WriteLine($"Sending message: {message}");

            var userMessage = new ChatMessage(ChatRole.User, message);
            ChatHistory.Add(userMessage);
            ChatResponse completion = await Client.GetResponseAsync(ChatHistory, ChatOptions);
            var assistantMessage = new ChatMessage(ChatRole.Assistant, completion.GetMessage().Text);
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
            ChatResponse completion = await Client.GetResponseAsync(tmpChatHistory);

            bool succeeded = bool.TryParse(completion.GetMessage().Text, out var result);
            if (!succeeded)
            {
                throw new Exception($"Natural language test failed to parse the result. Response was: {completion}");
            }

            _output.WriteLine($@"LLM ruled that the output {(result ? "DID" : "DID NOT")} match the following query: ""{expected}""");
            return result;
        }
    }
}

