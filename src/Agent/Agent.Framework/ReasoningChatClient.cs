// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public sealed class ReasoningChatClient : DelegatingChatClient
{
    public bool UseResponsesApi { get; }

    public ReasoningChatClient(IChatClient inner, bool useResponsesApi) : base(inner)
    {
        UseResponsesApi = useResponsesApi;
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
            configureForResponsesApi: UseResponsesApi);
    }
}
