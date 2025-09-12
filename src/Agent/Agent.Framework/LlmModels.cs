// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Immutable;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Framework;

public static class LlmModels
{
    public const string Gpt4o = "gpt-4o";
    public const string Gpt41 = "gpt-4.1";
    public const string Gpt5 = "gpt-5";

    private static readonly ImmutableArray<string> SupportedModelDeployments =
    [
        Gpt4o,
        Gpt41,
        Gpt5
    ];

    public static readonly ImmutableDictionary<string, string> LlmClients = SupportedModelDeployments
        .ToImmutableDictionary(
            keySelector: static model => model,
            elementSelector: GetClientName);

    private static string GetClientName(string modelName)
    {
        return $"{modelName}-client";
    }

    public static IChatClient GetChatClient(this IServiceProvider serviceProvider, string? modelName = null)
    {
        if (string.IsNullOrEmpty(modelName))
        {
            return serviceProvider.GetRequiredService<IChatClient>();
        }

        if (!LlmClients.TryGetValue(modelName, out var clientName))
        {
            throw new ArgumentException($"Could not retrieve ChatClient. Unsupported model: {modelName}");
        }

        return serviceProvider.GetRequiredKeyedService<IChatClient>(clientName);
    }
}
