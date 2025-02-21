// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using Agent.Core;
using FirstPartyAgent.Agents;

namespace FirstPartyAgent;

public static class ChatHistoryPersistency
{
    private static readonly AsyncReaderWriterLock _lock = new();
    private static readonly ChatHistory s_chatHistory;

    static ChatHistoryPersistency()
    {
        s_chatHistory = new ChatHistory();
        s_chatHistory.AddSystemMessage(ICMAgent.SystemMessage);
    }

    public static async Task<T> ChatHistoryTransition<T>(
        Func<ChatHistory, Task<T>> action)
    {
        // TODO: is chathistory thread safe? what happens if two model request was send upon the same chat history
        using var _ = await _lock.AcquireWriterAsync();

        return await action(s_chatHistory);
    }
}
