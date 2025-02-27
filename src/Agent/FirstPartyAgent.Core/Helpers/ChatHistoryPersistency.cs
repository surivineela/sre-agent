// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using Agent.Core;
using FirstPartyAgent.Agents;
using FirstPartyAgent.Models;

namespace FirstPartyAgent;

public static class ChatHistoryPersistency
{
    private static readonly AsyncReaderWriterLock _lock = new();
    private static readonly ChatHistory s_chatHistory;
    private static AgentMode _agentMode;
    private static bool _agentModeSetOnce = false;

    static ChatHistoryPersistency()
    {
        s_chatHistory = new ChatHistory();
        string agentModeStr = Environment.GetEnvironmentVariable("AgentMode") ?? string.Empty;
        var agentMode = Enum.TryParse<AgentMode>(agentModeStr, out var mode) ? mode : AgentMode.ICM;
    }

    private static void initAgentModeIfNotSet(AgentMode agentMode)
    {
        if (!_agentModeSetOnce)
        {
            _agentMode = agentMode;
            _agentModeSetOnce = true;
            SetSystemPrompt(agentMode);
        }
    }


    private static void SetSystemPrompt(AgentMode agentMode)
    {
        string systemPrompt = ICMAgent.SystemMessage;

        switch (agentMode)
        {
            case AgentMode.ACA:
                systemPrompt = ContainerAppAgent.GpuQuota.SystemMessage;
                break;
            case AgentMode.ICM:
                systemPrompt = ICMAgent.SystemMessage;
                break;
            case AgentMode.GithubIssueTagger:
                systemPrompt = GithubIssueTaggerAgent.SystemMessage;
                break;
            default:
                systemPrompt = ICMAgent.SystemMessage;
                break;
        }

        s_chatHistory.AddSystemMessage(systemPrompt);
    }

    public static async Task<T> ChatHistoryTransition<T>(AgentMode agentMode, 
        Func<ChatHistory, Task<T>> action)
    {
        // TODO: is chathistory thread safe? what happens if two model request was send upon the same chat history
        using var _ = await _lock.AcquireWriterAsync();
        initAgentModeIfNotSet(agentMode);
        return await action(s_chatHistory);
    }
}
