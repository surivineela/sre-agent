// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents;

namespace Agent.Runtime.Helpers;

public static class ApprovalHelper
{
    /// <summary>
    /// Generates a unique title for the approval request based on the thread ID, processor ID, operation name, and arguments.
    /// </summary>
    /// <param name="threadId">Thread ID</param>
    /// <param name="processorId">This is the agentContext ID</param>
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

    public static bool ToolRequiresApproval(IToolFunction tool)
    {
        var attribute = tool.ToolFunction.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();

        return attribute != null;
    }

    public static string GetToolDefaultApprovalMessage(IToolFunction tool)
    {
        var attribute = tool.ToolFunction.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();

        return attribute?.DisplayMessage ?? string.Empty;
    }

    public static bool ApprovalExpired(this Approval approval, IToolFunction tool)
    {
        var attribute = tool.ToolFunction.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();

        // should never happen
        if (attribute == null)
        {
            throw new InvalidOperationException($"Approval is not required for this tool {tool.ToolFunction.Name}");
        }

        if (approval.Status == ApprovalDecision.Approved &&
            (string.IsNullOrEmpty(approval.OboToken) || OboTokenExpired(approval.OboToken)))
        {
            return true;
        }

        return false;
    }

    private static bool OboTokenExpired(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        if (jsonToken == null)
        {
            return true;
        }

        var expiration = jsonToken.ValidTo;
        if (DateTime.UtcNow.AddMinutes(5) > expiration)
        {
            return true;
        }

        return false;
    }
}
