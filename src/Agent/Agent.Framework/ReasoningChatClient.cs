// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public abstract record ReasoningChatClientOptions();
public sealed record OpenAIReasoningChatClientOptions(bool UseResponsesApi) : ReasoningChatClientOptions;
public sealed record AnthropicReasoningChatClientOptions(string ModelId, bool ExtendedThinkingEnabled, int MaxOutputTokens, int ThinkingTokenBudget) : ReasoningChatClientOptions;


public sealed class ReasoningChatClient : DelegatingChatClient
{
    public ReasoningChatClientOptions Options { get; }
    public bool UseResponsesApi => Options is OpenAIReasoningChatClientOptions { UseResponsesApi: true };

    public ReasoningChatClient(IChatClient inner, ReasoningChatClientOptions options) : base(inner)
    {
        Options = options;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var o = Prepare(options);
        return base.GetResponseAsync(messages, o, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var o = Prepare(options);
        return base.GetStreamingResponseAsync(messages, o, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ChatOptions Prepare(ChatOptions? options)
    {
        options ??= new ChatOptions();
        return options.WithRawRepresentationFactory(
            chatClient: this,
            reasoningOptions: Options);
    }
}
