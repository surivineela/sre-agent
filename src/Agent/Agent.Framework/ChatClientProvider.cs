// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Framework;

public class ChatClientProvider
{
    private readonly IServiceProvider _serviceProvider;

    public ChatClientProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IChatClient? GetChatClient(string? modelName = null)
    {
        if (string.IsNullOrEmpty(modelName))
        {
            return null;
        }

        if (!LlmModels.LlmClients.TryGetValue(modelName, out var clientName))
        {
            throw new ArgumentException($"Could not retrieve ChatClient. Unsupported model: {modelName}. Supported models are: {string.Join(", ", LlmModels.LlmClients.Keys)}");
        }

        return _serviceProvider.GetRequiredKeyedService<IChatClient>(clientName);
    }
}
