// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework;

public class AgentTextTrajectoryItem : TrajectoryItem
{
    public string RoleDisplayName { get; }
    public string Text { get; }

    public AgentTextTrajectoryItem(string roleDisplayName, string text)
    {
        RoleDisplayName = roleDisplayName;
        Text = text;
    }

    public override string ToString(bool filterResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Role: {RoleDisplayName}");
        sb.AppendLine(Text);
        sb.AppendLine();
        return sb.ToString();
    }
}

public class AgentReasoningTrajectoryItem : TrajectoryItem
{
    public string RoleDisplayName { get; }
    public string ReasoningText { get; }

    public AgentReasoningTrajectoryItem(string roleDisplayName, string text)
    {
        RoleDisplayName = roleDisplayName;
        ReasoningText = text;
    }

    public override string ToString(bool filterResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Role: {RoleDisplayName}");
        sb.AppendLine($"Internal Reasoning: {ReasoningText}");
        sb.AppendLine();
        return sb.ToString();
    }
}

public class AgentFunctionCallTrajectoryItem : TrajectoryItem
{
    public string RoleDisplayName { get; }
    public string FunctionName { get; }
    public string Parameters { get; }

    public AgentFunctionCallTrajectoryItem(string roleDisplayName, string functionName, string parameters)
    {
        RoleDisplayName = roleDisplayName;
        FunctionName = functionName;
        Parameters = parameters;
    }

    public override string ToString(bool filterResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Role: {RoleDisplayName}");
        sb.AppendLine($"Function Call: {FunctionName}");
        sb.AppendLine($"Parameters: {Parameters}");
        sb.AppendLine();
        return sb.ToString();
    }
}

public sealed class AgentTrajectory
{
    private const string TransferToolStart = "transfer_to_";
    private const string HandOffToolName = "handoffback";

    private readonly bool _autoHandoffEnabled;
    private readonly string _startingAgent;
    private readonly ILogger _logger;

    private List<string> _agentStack;
    private string _activeHandoff = string.Empty;

    // Track critic count per agent name instead of globally
    private readonly Dictionary<string, int> _agentCriticCounts = [];

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private const int MaxResultWords = 200;

    private readonly List<TrajectoryItem> _trajectoryItems = [];

    public AgentTrajectory(string startingAgent, bool autoHandOffToStartEnabled, ILogger logger)
    {
        _startingAgent = startingAgent;
        _agentStack = [startingAgent];
        _autoHandoffEnabled = autoHandOffToStartEnabled;
        _logger = logger;
    }

    public AgentTrajectory(IReadOnlyList<string> initialAgentStack, bool autoHandOffToStartEnabled, ILogger logger)
    {
        _startingAgent = initialAgentStack[0];
        _agentStack = [.. initialAgentStack];
        _autoHandoffEnabled = autoHandOffToStartEnabled;
        _logger = logger;
    }

    public IReadOnlyList<string> AgentStack => _agentStack;

    /// <summary>
    /// Gets the critic count for a specific agent
    /// </summary>
    public int GetCriticCount(string agentName)
    {
        return _agentCriticCounts.GetValueOrDefault(agentName, 0);
    }

    /// <summary>
    /// Increments the critic count for a specific agent when the critic actually runs
    /// </summary>
    public void IncrementCriticCount(string agentName)
    {
        _agentCriticCounts[agentName] = _agentCriticCounts.GetValueOrDefault(agentName, 0) + 1;
    }

    /// <summary>
    /// Resets critic counts for all agents (called when conversation completes successfully)
    /// </summary>
    public void ResetCriticCounts()
    {
        _agentCriticCounts.Clear();
    }

    public void Append(ChatResponse modelResponse)
    {
        foreach (var msg in modelResponse.Messages)
        {
            Append(msg);
        }
    }

    public void Append(ChatMessage message)
    {
        // ignore system messages.. they are not part of trajectory, rather guidances
        if (message.Role == ChatRole.System)
        {
            return;
        }

        foreach (var content in message.Contents)
        {
            if (content is TextContent textContent)
            {
                var text = message.Role == ChatRole.User
                    ? Summarizer.ExtractUserQuestion(textContent.Text)
                    : textContent.Text;

                var activeRole = message.Role == ChatRole.User
                    ? "user"
                    : _agentStack[^1];

                _trajectoryItems.Add(new AgentTextTrajectoryItem(activeRole, text));

                if (_autoHandoffEnabled
                    && IsEndOfTurnMessage(message))
                {
                    _agentStack = [_startingAgent];
                }
            }
            else if (content is FunctionCallContent functionCallContent)
            {
                var activeAgent = _agentStack[^1];

                _trajectoryItems.Add(new AgentFunctionCallTrajectoryItem(
                    roleDisplayName: activeAgent,
                    functionName: functionCallContent.Name,
                    parameters: functionCallContent.GetSerializedArguments()));

                // save the last attempted handoff
                if (functionCallContent.Name.StartsWith(TransferToolStart, StringComparison.OrdinalIgnoreCase))
                {
                    _activeHandoff = functionCallContent.Name.Substring(TransferToolStart.Length);
                }
                else if (functionCallContent.Name.Equals(HandOffToolName, StringComparison.OrdinalIgnoreCase))
                {
                    _activeHandoff = HandOffToolName;
                }
            }
            // don't expect this in general as tool calls are handled manually
            // however for parallel tool call we use functionInvokingChatClient, which will inline the results
            else if (content is FunctionResultContent functionResultContent)
            {
                Append(functionResultContent);

                var resultString = string.Empty;
                if (functionResultContent.Result is string fResult)
                {
                    resultString = fResult;
                }
                else if (functionResultContent.Result is JsonElement u
                    && u.ValueKind == JsonValueKind.String)
                {
                    resultString = u.GetString()!;
                }

                // execute the saved handoff if approved.
                if (resultString.StartsWith(Handoff<string>.GetTransferMessage(), StringComparison.OrdinalIgnoreCase))
                {
                    if (_activeHandoff == HandOffToolName)
                    {
                        _agentStack.RemoveAt(_agentStack.Count - 1);
                    }
                    else
                    {
                        _agentStack.Add(_activeHandoff);
                    }
                }
            }
            else if (content is TextReasoningContent textReasoningContent)
            {
                var activeAgent = _agentStack[^1];
                var reasoningText = textReasoningContent.Text;
                _trajectoryItems.Add(new AgentReasoningTrajectoryItem(activeAgent, reasoningText));
            }
            else
            {
                _logger.LogInternalError($"Unknown content type in trajectory: {content.GetType()}");
            }
        }
    }

    public static bool IsEndOfTurnMessage(ChatMessage message)
    {
        if (message.Role == ChatRole.Assistant
            && message.Contents.Last() is TextContent t)
        {
            try
            {
                var op = JsonSerializer.Deserialize<Dictionary<string, string>>(t.Text);
                if (op is not null
                    && op.TryGetValue("state", out var reasoningState)
                    && (
                        string.Equals("CompletedSuccessfully", reasoningState, StringComparison.OrdinalIgnoreCase)
                        || string.Equals("RequestFailed", reasoningState, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    public void Append(FunctionResultContent functionResult)
    {
        _trajectoryItems.Add(new FunctionResultTrajectoryItem(functionResult.CallId, functionResult.Result));
    }

    public static string ResultToString(FunctionResultContent functionResult)
    {
        if (functionResult.Result is null)
        {
            return "null";
        }
        else
        {
            var resultObj = functionResult.Result;

            var resultString = (resultObj is string str) ? str : JsonSerializer.Serialize(resultObj, _jsonOptions);

            return TextVolumeHelpers.ApplyWordTruncation(
                input: resultString,
                maxWords: MaxResultWords);
        }
    }

    public string GetFilteredTrajectory()
    {
        // Apply filtering strategy: keep all content but filter function results to reduce context size, and return the trajectory
        var trajectory = ToString(filterResults: true);

        // Remove function result items from memory to reduce memory usage
        _trajectoryItems.RemoveAll(item => item is FunctionResultTrajectoryItem);

        return trajectory;
    }

    /// <summary>
    /// Get the full trajectory without any filtering (for debugging or other purposes)
    /// </summary>
    public string GetFullTrajectory()
    {
        return ToString(filterResults: false);
    }

    private string ToString(bool filterResults)
    {
        var sb = new StringBuilder();
        foreach (var item in _trajectoryItems)
        {
            sb.Append(item.ToString(filterResults));
        }
        return sb.ToString();
    }
}
