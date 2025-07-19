// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public abstract class TrajectoryItem
{
    public abstract string ToString(bool filterResults);
}

public class TextTrajectoryItem : TrajectoryItem
{
    public ChatRole Role { get; }
    public string Text { get; }

    public TextTrajectoryItem(ChatRole role, string text)
    {
        Role = role;
        Text = role == ChatRole.User
            ? Summarizer.ExtractUserQuestion(text)
            : text;
    }

    public override string ToString(bool filterResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Role: {Role}");
        sb.AppendLine();
        sb.AppendLine(Text);
        sb.AppendLine();
        return sb.ToString();
    }
}

public class FunctionCallTrajectoryItem : TrajectoryItem
{
    public ChatRole Role { get; }
    public string FunctionName { get; }
    public string Parameters { get; }

    public FunctionCallTrajectoryItem(ChatRole role, string functionName, string parameters)
    {
        Role = role;
        FunctionName = functionName;
        Parameters = parameters;
    }

    public override string ToString(bool filterResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Role: {Role}");
        sb.AppendLine();
        sb.AppendLine($"Function Call: {FunctionName}");
        sb.AppendLine($"Parameters: {Parameters}");
        sb.AppendLine();
        return sb.ToString();
    }
}

public class FunctionResultTrajectoryItem : TrajectoryItem
{
    private static JsonSerializerOptions _jsonOptions = AIJsonUtilities.DefaultOptions;

    public string CallId { get; }
    public object? Result { get; }

    public FunctionResultTrajectoryItem(string callId, object? result)
    {
        CallId = callId;
        Result = result;
    }

    public override string ToString(bool filterResults)
    {
        var sb = new StringBuilder();
        if (filterResults)
        {
            // When filtering, only show that a function result was received but not the content
            sb.AppendLine("Function Call Result: [Result filtered for brevity]");
        }
        else
        {
            // Full result content
            var functionResult = new FunctionResultContent(CallId, Result);
            var resultString = ResultToString(functionResult);
            sb.AppendLine($"Function Call Result:\n{resultString}");
        }
        sb.AppendLine();
        return sb.ToString();
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

            var resultString = resultObj is string str
                ? str
                : JsonSerializer.Serialize(resultObj, _jsonOptions);

            return TextVolumeHelpers.ApplyWordTruncation(
                input: resultString,
                maxWords: 200,
                addTruncationMessage: false);
        }
    }
}

public class CriticFeedbackTrajectoryItem : TrajectoryItem
{
    public string Feedback { get; }

    public CriticFeedbackTrajectoryItem(string feedback)
    {
        Feedback = feedback;
    }

    public override string ToString(bool filterResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"This is the critic feedback received by the agent in the past run. It contains a verified summary of the past actions taken by the agent.\n\nRole: Critic.");
        sb.AppendLine();
        sb.AppendLine(Feedback);
        sb.AppendLine($"Now the agent has taken the following steps since then to address the feedback.\n\n");
        return sb.ToString();
    }
}

public sealed class Trajectory
{
    // Track critic count per agent name instead of globally
    private readonly Dictionary<string, int> _agentCriticCounts = new();

    private readonly List<TrajectoryItem> _trajectoryItems = new();

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
        foreach (var content in message.Contents)
        {
            if (content is TextContent textContent)
            {
                _trajectoryItems.Add(new TextTrajectoryItem(message.Role, textContent.Text));
            }
            else if (content is FunctionCallContent functionCallContent)
            {
                var parameters = "";
                if (functionCallContent.RawRepresentation is not null)
                {
                    parameters = (functionCallContent.RawRepresentation as OpenAI.Chat.ChatToolCall)!.FunctionArguments.ToString();
                }
                else if (functionCallContent.Arguments is not null)
                {
                    parameters = JsonSerializer.Serialize(functionCallContent.Arguments);
                }
                _trajectoryItems.Add(new FunctionCallTrajectoryItem(message.Role, functionCallContent.Name, parameters));
            }
            // don't expect this in general as tool calls are handled manually
            // however for parallel tool call we use functionInvokingChatClient, which will inline the results
            else if (content is FunctionResultContent functionResultContent)
            {
                Append(functionResultContent);
            }
            else
            {
                throw new Exception($"Unknown content type: {content.GetType()}");
            }
        }
    }

    public void Append(FunctionResultContent functionResult)
    {
        _trajectoryItems.Add(new FunctionResultTrajectoryItem(functionResult.CallId, functionResult.Result));
    }

    public void AppendCriticFeedback(string feedback)
    {
        _trajectoryItems.Add(new CriticFeedbackTrajectoryItem(feedback));
    }

    /// <summary>
    /// Creates a new Trajectory instance from chat message history
    /// </summary>
    /// <param name="chatMessages">The chat messages to restore from</param>
    /// <returns>A new Trajectory with items built from the chat messages</returns>
    public static Trajectory FromChatHistory(IEnumerable<ChatMessage> chatMessages)
    {
        var trajectory = new Trajectory();

        foreach (var message in chatMessages)
        {
            // Handle different message roles appropriately
            if (message.Role == ChatRole.System || message.Role == ChatRole.User || message.Role == ChatRole.Assistant)
            {
                foreach (var content in message.Contents)
                {
                    if (content is TextContent textContent)
                    {
                        trajectory._trajectoryItems.Add(new TextTrajectoryItem(message.Role, textContent.Text));
                    }
                    else if (content is FunctionCallContent functionCallContent)
                    {
                        var parameters = (functionCallContent.RawRepresentation as OpenAI.Chat.ChatToolCall)?.FunctionArguments.ToString() ?? "{}";
                        trajectory._trajectoryItems.Add(new FunctionCallTrajectoryItem(message.Role, functionCallContent.Name, parameters));
                    }
                    else if (content is FunctionResultContent functionResultContent)
                    {
                        trajectory._trajectoryItems.Add(new FunctionResultTrajectoryItem(functionResultContent.CallId, functionResultContent.Result));
                    }
                    // Skip unknown content types when restoring from history
                }
            }
            else if (message.Role == ChatRole.Tool)
            {
                // Tool messages should only contain function result content
                foreach (var content in message.Contents)
                {
                    if (content is FunctionResultContent functionResultContent)
                    {
                        trajectory._trajectoryItems.Add(new FunctionResultTrajectoryItem(functionResultContent.CallId, functionResultContent.Result));
                    }
                    // Tool messages might also have text content in some cases
                    else if (content is TextContent textContent)
                    {
                        trajectory._trajectoryItems.Add(new TextTrajectoryItem(message.Role, textContent.Text));
                    }
                }
            }
            else
            {
                // For any other roles, treat them as text content
                foreach (var content in message.Contents)
                {
                    if (content is TextContent textContent)
                    {
                        trajectory._trajectoryItems.Add(new TextTrajectoryItem(message.Role, textContent.Text));
                    }
                }
            }
        }

        return trajectory;
    }

    /// <summary>
    /// Detects if there's a handoff loop pattern in the trajectory
    /// </summary>
    /// <param name="loopThreshold">Number of transfer->handoffback cycles to consider as a loop</param>
    /// <returns>True if a handoff loop is detected</returns>
    public bool HasHandoffLoop(int loopThreshold = 3)
    {
        var handoffEvents = new List<(string eventType, string? agentName)>();

        // Extract handoff patterns from trajectory items
        foreach (var item in _trajectoryItems)
        {
            if (item is FunctionCallTrajectoryItem fcItem)
            {
                // Check if it's a transfer_to_* call
                if (fcItem.FunctionName.StartsWith("transfer_to_", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to extract agent name from function name (e.g., "transfer_to_AgentB" -> "AgentB")
                    var agentName = fcItem.FunctionName.Substring("transfer_to_".Length);
                    handoffEvents.Add(("transfer", agentName));
                }
                else if (fcItem.FunctionName.Equals("handoffback", StringComparison.OrdinalIgnoreCase))
                {
                    handoffEvents.Add(("handoffback", null));
                }
            }
        }

        return DetectLoopPattern(handoffEvents, loopThreshold);
    }

    /// <summary>
    /// Detects repeating transfer->handoffback patterns
    /// </summary>
    private bool DetectLoopPattern(List<(string eventType, string? agentName)> handoffEvents, int loopThreshold)
    {
        if (handoffEvents.Count < loopThreshold * 2)
        {
            return false;
        }

        int consecutivePatterns = 0;
        string? lastTransferAgent = null;

        for (int i = 0; i < handoffEvents.Count - 1; i++)
        {
            var current = handoffEvents[i];
            var next = i + 1 < handoffEvents.Count ? handoffEvents[i + 1] : (eventType: "", agentName: null);

            if (current.eventType == "transfer" && next.eventType == "handoffback")
            {
                // Check if we're transferring to the same agent repeatedly
                if (lastTransferAgent == current.agentName && lastTransferAgent != null)
                {
                    consecutivePatterns++;

                    if (consecutivePatterns >= loopThreshold - 1) // -1 because we count pairs
                    {
                        return true;
                    }
                }
                else
                {
                    lastTransferAgent = current.agentName;
                    consecutivePatterns = 1;
                }

                i++; // Skip the handoffback since we've processed the pair
            }
            else
            {
                // Pattern broken, reset
                consecutivePatterns = 0;
                lastTransferAgent = null;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a summary of recent handoff activity for debugging
    /// </summary>
    public string GetHandoffSummary()
    {
        var handoffs = new List<string>();

        foreach (var item in _trajectoryItems)
        {
            if (item is FunctionCallTrajectoryItem fcItem)
            {
                if (fcItem.FunctionName.StartsWith("transfer", StringComparison.OrdinalIgnoreCase) ||
                    fcItem.FunctionName.Equals("handoffback", StringComparison.OrdinalIgnoreCase))
                {
                    handoffs.Add(fcItem.FunctionName);
                }
            }
        }

        return $"Recent handoffs: {string.Join(" → ", handoffs.TakeLast(10))}";
    }

    public string GetFilteredTrajectory()
    {
        // Apply filtering strategy: keep all content but filter function results to reduce context size, and return the trajectory
        var trajectory = ToString(filterResults: true);

        // Remove function result items from memory to reduce memory usage
        _trajectoryItems.RemoveAll(item => item is FunctionResultTrajectoryItem);

        // NOTE: CriticCount should be incremented only when critic actually runs, not here
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
