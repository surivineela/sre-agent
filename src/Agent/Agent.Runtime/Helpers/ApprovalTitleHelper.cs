// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Agent.Runtime.Helpers;

public static class ApprovalTitleHelper
{
    /// <summary>
    /// Generates a unique title for the approval request based on the thread ID, processor ID, operation name, and arguments.
    /// </summary>
    /// <param name="threadId">Thread ID</param>
    /// <param name="processorId">For durable-agents, this is the orchestration ID. Otherwise, this is the agentContext ID</param>
    /// <param name="operationName">Name of the target function</param>
    /// <param name="arguments">Function arguments</param>
    /// <returns></returns>
    public static string GenerateUniqueApprovalTitle(string threadId, string processorId, string operationName, IDictionary<string, object?> arguments)
    {
        if (operationName.Equals("PatchKubernetesYaml", StringComparison.OrdinalIgnoreCase))
        {
            // The PatchKubernetesYaml function is a special case where the argument yamlContent is not very stable currently.
            return $"{threadId}-{processorId}-{operationName}-abcdefg";
        }

        // calculate SHA256 hash of the arguments
        var orderedArgs = new OrderedDictionary<string, object?>(arguments);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderedArgs)));
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        var truncatedHash = hashString.Substring(0, Math.Min(16, hashString.Length));

        return $"{threadId}-{processorId}-{operationName}-{truncatedHash}";
    }
}
