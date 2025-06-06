using System;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Agent.Plugins.Definitions;
using Agent.Runtime.Models;
using Agent.Runtime.SubAgents;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;

namespace Agent.Tests.Common.Mocks.FunctionCalling;

public class ReplayEntry
{
    public string CallId { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string FunctionArgumentsJson { get; set; } = string.Empty;
    public string FunctionResultJson { get; set; } = string.Empty;
}

/// <summary>
/// Core replay functionality that can be shared between different tool interfaces.
/// Handles loading logs, storing replay entries, and creating replayed tool functions.
/// </summary>
public class ReplayToolCore
{
    private readonly List<ReplayEntry> _replayEntries = new();
    private readonly List<ReplayEntry> _incompleteCallEntries = new();
    private readonly HashSet<string> _loggedCallIdsWithNoResult = new();
    private readonly JsonSerializerOptions _serializerOptions;

    public List<string> FunctionNamesSkippedForReplay { get; } = new();
    public List<ReplayEntry> FunctionCallsWithReplayFailure { get; } = new();
    public HashSet<string> FunctionNamesEnabledForReplay { get; } = new();

    public ReplayToolCore(JsonSerializerOptions serializerOptions)
    {
        _serializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
    }

    public void LoadLogFromString(string logContent)
    {
        if (logContent == null)
        {
            throw new ArgumentNullException(nameof(logContent));
        }

        if (string.IsNullOrWhiteSpace(logContent))
        {
            throw new ArgumentException("Log content cannot be empty or whitespace.", nameof(logContent));
        }

        ProcessLogContent(logContent);
        AddFunctionNamesForReplay(FunctionNames);
    }

    private void AddFunctionNamesForReplay(IEnumerable<string> functionNames)
    {
        if (functionNames == null) return;

        var specialMethods = typeof(UserInteractionPluginDefinition).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(AgentControlFlowPluginDefinition).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(x => x.GetCustomAttribute<DescriptionAttribute>() != null)
            .Select(x => x.Name)
            .ToList();

        foreach (var name in functionNames)
        {
            if (name.StartsWith("transfer_", StringComparison.OrdinalIgnoreCase)
                || specialMethods.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                FunctionNamesSkippedForReplay.Add(name);
            }
            else
            {
                FunctionNamesEnabledForReplay.Add(name);
            }
        }
    }

    public IEnumerable<string> FunctionNames => _replayEntries.Select(x => x.FunctionName).Distinct();

    /// <summary>
    /// Checks if a function with the given name has replay data available.
    /// </summary>
    public bool HasReplayDataForFunction(string functionName)
    {
        if (string.IsNullOrEmpty(functionName))
        {
            return false;
        }

        if (!FunctionNamesEnabledForReplay.Contains(functionName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return _replayEntries.Any(e => e.FunctionName == functionName);
    }

    /// <summary>
    /// Creates a replay wrapper around an existing AIFunction that intercepts calls
    /// to check for cached results before falling back to the original function.
    /// </summary>
    public AIFunction CreateReplayWrapper(string functionName, AIFunction originalFunction)
    {
        //// Check if this function is permitted for replay
        //if (!FunctionNamesEnabledForReplay.Contains(functionName, StringComparer.OrdinalIgnoreCase))
        //{
        //    return originalFunction; // Return original function without wrapping
        //}

        //// Check if we have any replay data for this function
        //var hasReplayData = _replayEntries.Any(e => e.FunctionName == functionName);
        //if (!hasReplayData)
        //{
        //    return originalFunction; // Return original function without wrapping
        //}

        return new ReplayAIFunctionWrapper(originalFunction, this, functionName);
    }

    /// <summary>
    /// Public method to serialize arguments for replay matching.
    /// </summary>
    public string SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments == null) return "null";
        return JsonSerializer.Serialize(arguments, _serializerOptions);
    }

    /// <summary>
    /// Public method to find a replay match for given function name and arguments.
    /// </summary>
    public ReplayEntry? FindReplayMatch(string functionName, string inputArgsJson)
    {
        return _replayEntries.FirstOrDefault(e =>
            e.FunctionName == functionName &&
            AreJsonStringsEquivalent(e.FunctionArgumentsJson, inputArgsJson));
    }

    /// <summary>
    /// Public accessor for serializer options.
    /// </summary>
    public JsonSerializerOptions SerializerOptions => _serializerOptions;

    private void ProcessLogContent(string logContent)
    {
        var chatMessages = DeserializeChatMessages(logContent);
        if (chatMessages == null) return;

        var currentLogData = ExtractLogData(chatMessages);
        MergeLogDataIntoRepository(currentLogData);
    }

    private ChatMessage[]? DeserializeChatMessages(string logContent)
    {
        // log might be a string serialized to JSON. If so, handle that.
        if (logContent.StartsWith('"'))
        {
            logContent = JsonSerializer.Deserialize<string>(logContent) ?? string.Empty;
        }        // Create local serialization options to handle different naming policies without mutating global state
        var deserializationOptions = _serializerOptions;

        // Check if the log uses Pascal case naming (legacy format) and create appropriate options
        if (logContent.Contains("\"AuthorName\":", StringComparison.InvariantCulture))
        {
            deserializationOptions = new JsonSerializerOptions(_serializerOptions)
            {
                PropertyNamingPolicy = null // Use Pascal case for legacy logs
            };
        }

        return JsonSerializer.Deserialize<ChatMessage[]>(logContent, deserializationOptions);
    }

    private (List<ReplayEntry> entries, HashSet<string> incompleteCallIds) ExtractLogData(ChatMessage[] chatMessages)
    {
        if (chatMessages.Length == 0)
        {
            throw new ArgumentException("Chat message array is empty. Unable to extract log data.");
        }

        if (chatMessages.First().Contents.Count == 0)
        {
            throw new Exception("No contents in ChatMessage, this is likely an issue with the log format or deserialization options, particularly a property naming case mismatch.");
        }

        var pendingFunctionCalls = new Dictionary<string, (string Name, IDictionary<string, object?>? Arguments)>();
        var currentLogReplayEntries = new List<ReplayEntry>();
        var currentLogLoggedCallIdsWithNoResult = new HashSet<string>();

        foreach (var message in chatMessages)
        {
            if (message.Contents == null) continue;
            ProcessMessageContents(message.Contents, pendingFunctionCalls, currentLogReplayEntries);
        }

        IdentifyIncompleteCallIds(pendingFunctionCalls, currentLogReplayEntries, currentLogLoggedCallIdsWithNoResult);

        return (currentLogReplayEntries, currentLogLoggedCallIdsWithNoResult);
    }

    private void ProcessMessageContents(
        IEnumerable<AIContent> contents,
        Dictionary<string, (string Name, IDictionary<string, object?>? Arguments)> pendingFunctionCalls,
        List<ReplayEntry> currentLogReplayEntries)
    {
        foreach (var contentItem in contents)
        {
            switch (contentItem)
            {
                case FunctionCallContent callContent when !string.IsNullOrEmpty(callContent.CallId) && !string.IsNullOrEmpty(callContent.Name):
                    pendingFunctionCalls[callContent.CallId] = (callContent.Name, callContent.Arguments);
                    break;

                case FunctionResultContent resultContent when !string.IsNullOrEmpty(resultContent.CallId) && resultContent.Result != null:
                    ProcessFunctionResult(resultContent, pendingFunctionCalls, currentLogReplayEntries);
                    break;
            }
        }
    }

    private void ProcessFunctionResult(
        FunctionResultContent resultContent,
        Dictionary<string, (string Name, IDictionary<string, object?>? Arguments)> pendingFunctionCalls,
        List<ReplayEntry> currentLogReplayEntries)
    {
        if (!pendingFunctionCalls.TryGetValue(resultContent.CallId, out var callInfo))
            return;

        string argsJsonForEntry = SerializeArguments(callInfo.Arguments);
        string resultJsonForEntry = SerializeFunctionResult(resultContent.Result);

        currentLogReplayEntries.Add(new ReplayEntry
        {
            CallId = resultContent.CallId,
            FunctionName = callInfo.Name,
            FunctionArgumentsJson = argsJsonForEntry,
            FunctionResultJson = resultJsonForEntry
        });
    }    private string SerializeFunctionResult(object? result)
    {        return result switch
        {
            null => "null",
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String =>
                JsonSerializer.Serialize(jsonElement.GetString() ?? string.Empty, _serializerOptions),
            JsonElement jsonElement =>
                JsonSerializer.Serialize(jsonElement, _serializerOptions),
            string s => s,
            _ => JsonSerializer.Serialize(result, _serializerOptions)
        };
    }    private void IdentifyIncompleteCallIds(
        Dictionary<string, (string Name, IDictionary<string, object?>? Arguments)> pendingFunctionCalls,
        List<ReplayEntry> currentLogReplayEntries,
        HashSet<string> currentLogLoggedCallIdsWithNoResult)
    {
        var callIdsWithResultsInCurrentLog = new HashSet<string>(currentLogReplayEntries.Select(entry => entry.CallId));
        foreach (var (callId, (name, arguments)) in pendingFunctionCalls)
        {
            if (!callIdsWithResultsInCurrentLog.Contains(callId))
            {
                currentLogLoggedCallIdsWithNoResult.Add(callId);

                // Create an entry for the incomplete call to track function name and arguments
                var incompleteEntry = new ReplayEntry
                {
                    CallId = callId,
                    FunctionName = name,
                    FunctionArgumentsJson = SerializeArguments(arguments),
                    FunctionResultJson = string.Empty // No result for incomplete calls
                };

                _incompleteCallEntries.Add(incompleteEntry);
            }
        }
    }

    private void MergeLogDataIntoRepository((List<ReplayEntry> entries, HashSet<string> incompleteCallIds) currentLogData)
    {
        foreach (var newEntry in currentLogData.entries)
        {
            MergeReplayEntry(newEntry);
        }

        UpdateIncompleteCallIds(currentLogData.entries, currentLogData.incompleteCallIds);
    }

    private void MergeReplayEntry(ReplayEntry newEntry)
    {
        var existingEntryIndex = _replayEntries.FindIndex(e =>
            e.FunctionName == newEntry.FunctionName &&
            AreJsonStringsEquivalent(e.FunctionArgumentsJson, newEntry.FunctionArgumentsJson));

        if (existingEntryIndex != -1)
        {
            var existingEntry = _replayEntries[existingEntryIndex];

            // commented out the below to avoid conflicts, last one wins

            //if (!AreJsonStringsEquivalent(existingEntry.FunctionResultJson, newEntry.FunctionResultJson))
            //{
            //    throw new InvalidOperationException(
            //        $"Ambiguous replay log: Function '{newEntry.FunctionName}' with arguments '{newEntry.FunctionArgumentsJson}' " +
            //        $"is defined with different results. Existing result: '{existingEntry.FunctionResultJson}', New result: '{newEntry.FunctionResultJson}'.");
            //}
            _replayEntries[existingEntryIndex] = newEntry;
        }
        else
        {
            _replayEntries.Add(newEntry);
        }
    }

    private void UpdateIncompleteCallIds(List<ReplayEntry> currentLogEntries, HashSet<string> currentLogIncompleteCallIds)
    {
        _loggedCallIdsWithNoResult.RemoveWhere(id => currentLogEntries.Any(re => re.CallId == id));
        foreach (var callId in currentLogIncompleteCallIds)
        {        if (!_replayEntries.Any(re => re.CallId == callId && !string.IsNullOrEmpty(re.FunctionResultJson)))
            {
                _loggedCallIdsWithNoResult.Add(callId);
            }
        }
    }    /// <summary>
    /// Validates that there are no incomplete calls for this function and arguments combination.
    /// Throws InvalidOperationException if any incomplete calls are found.
    /// </summary>
    public void ValidateNoIncompleteCallsForFunction(string functionName, IDictionary<string, object?>? arguments)
    {
        if (string.IsNullOrEmpty(functionName))
            return;

        string inputArgsJson = SerializeArguments(arguments);

        // Check if any incomplete calls exist for this function with these arguments
        // We need to check against the incomplete calls that were identified during log loading
        // These are stored in _loggedCallIdsWithNoResult but we need to find the ones that match
        // the function name and arguments by looking at the original incomplete function calls

        // For incomplete calls, we need to track the function name and arguments during log loading
        // Let's check if we have any incomplete calls with matching function name and arguments
        var hasIncompleteCallForFunction = _incompleteCallEntries.Any(entry =>
            entry.FunctionName == functionName &&
            AreJsonStringsEquivalent(entry.FunctionArgumentsJson, inputArgsJson));

        if (hasIncompleteCallForFunction)
        {
            var matchingEntry = _incompleteCallEntries.First(entry =>
                entry.FunctionName == functionName &&
                AreJsonStringsEquivalent(entry.FunctionArgumentsJson, inputArgsJson));

            throw new InvalidOperationException(
                $"Call with ID '{matchingEntry.CallId}' for function '{functionName}' was found in logs but without a corresponding result.");
        }
    }

    /// <summary>
    /// Public accessor for validating a specific function call is not incomplete.
    /// </summary>
    public void ValidateCallNotIncomplete(FunctionCallContent functionCall)
    {
        if (!string.IsNullOrEmpty(functionCall.CallId) && _loggedCallIdsWithNoResult.Contains(functionCall.CallId))
        {
            throw new InvalidOperationException(
                $"Call with ID '{functionCall.CallId}' for function '{functionCall.Name}' was found in logs but without a corresponding result.");
        }
    }

    /// <summary>
    /// Debug method to get all replay entries for a function name.
    /// </summary>
    public IEnumerable<ReplayEntry> GetReplayEntriesForFunction(string functionName)
    {
        return _replayEntries.Where(e => e.FunctionName == functionName);
    }

    private static bool AreJsonStringsEquivalent(string jsonStr1, string jsonStr2)
    {
        if (jsonStr1 == jsonStr2) return true;

        try
        {
            var node1 = JsonNode.Parse(jsonStr1);
            var node2 = JsonNode.Parse(jsonStr2);
            return JsonNode.DeepEquals(node1, node2);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
