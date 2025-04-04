// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Helpers;

public static class TrackedActionHelper

{
    private static readonly string LogFilePath = Path.Combine(
        Directory.GetParent(AppContext.BaseDirectory)!.FullName,
        "agent_actions.json"
    );
    private static readonly object FileLock = new();

    private static List<TrackedAction> LoadActions()
    {
        if (!File.Exists(LogFilePath))
            return new List<TrackedAction>();

        lock (FileLock)
        {
            var json = File.ReadAllText(LogFilePath);
            return string.IsNullOrEmpty(json) 
                ? new List<TrackedAction>() 
                : JsonSerializer.Deserialize<List<TrackedAction>>(json) ?? new List<TrackedAction>();
        }
    }

    private static void SaveActions(List<TrackedAction> actions)
    {
        lock (FileLock)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(LogFilePath, JsonSerializer.Serialize(actions, options));
        }
    }

    public static void TrackAction(
        string agentId,
        string resourceId,
        ActionType type,
        string description,
        Dictionary<string, string>? metadata = null,
        RemediationContext? remediationContext = null,
        ILogger? logger = null)
    {
        try
        {
            var action = new TrackedAction
            {
                AgentId = agentId,
                ResourceId = resourceId,
                Type = type,
                Status = ActionStatus.Initiated,
                Description = description,
                Metadata = metadata ?? new Dictionary<string, string>(),
                RemediationContext = remediationContext
            };

            var actions = LoadActions();
            actions.Add(action);
            SaveActions(actions);

            logger?.LogInformation(
                "Tracked action {ActionId} of type {ActionType} for resource {ResourceId}",
                action.ActionId, action.Type, action.ResourceId);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to track action");
        }
    }

    public static void UpdateActionStatus(
        string actionId,
        ActionStatus newStatus,
        DiagnosticEvent? diagnosticEvent = null,
        ILogger? logger = null)
    {
        try
        {
            var actions = LoadActions();
            var action = actions.FirstOrDefault(a => a.ActionId == actionId);
            
            if (action != null)
            {
                action.Status = newStatus;
                if (diagnosticEvent != null)
                {
                    action.DiagnosticEvents.Add(diagnosticEvent);
                }
                SaveActions(actions);

                logger?.LogInformation(
                    "Updated action {ActionId} status to {Status}",
                    actionId, newStatus);
            }
            else
            {
                logger?.LogWarning("Action {ActionId} not found", actionId);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to update action status");
        }
    }

    public static void AddDiagnosticEvent(
        string actionId,
        string eventType,
        string message,
        Dictionary<string, double>? metrics = null,
        Dictionary<string, string>? properties = null,
        ILogger? logger = null)
    {
        try
        {
            var diagnosticEvent = new DiagnosticEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Message = message,
                Metrics = metrics ?? new Dictionary<string, double>(),
                Properties = properties ?? new Dictionary<string, string>()
            };

            var actions = LoadActions();
            var action = actions.FirstOrDefault(a => a.ActionId == actionId);
            
            if (action != null)
            {
                action.DiagnosticEvents.Add(diagnosticEvent);
                SaveActions(actions);

                logger?.LogInformation(
                    "Added diagnostic event to action {ActionId}: {EventType}",
                    actionId, eventType);
            }
            else
            {
                logger?.LogWarning("Action {ActionId} not found", actionId);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to add diagnostic event");
        }
    }

    public static List<TrackedAction> GetActions(
        string? agentId = null,
        string? resourceId = null,
        ActionType? type = null,
        ActionStatus? status = null)
    {
        var actions = LoadActions();
        
        return actions.Where(a =>
            (agentId == null || a.AgentId == agentId) &&
            (resourceId == null || a.ResourceId == resourceId) &&
            (type == null || a.Type.Equals(type)) &&
            (status == null || a.Status == status)
        ).ToList();
    }
}
