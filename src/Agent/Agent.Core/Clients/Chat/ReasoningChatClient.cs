// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.CompilerServices;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Core.Clients.Chat;

public sealed class ReasoningChatClient : DelegatingChatClient
{
    public ReasoningChatClient(IChatClient inner) : base(inner) { }

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
    private static ChatOptions Prepare(ChatOptions? options)
    {
        options ??= new ChatOptions();
        return options.WithRawRepresentationFactory();
    }
}
