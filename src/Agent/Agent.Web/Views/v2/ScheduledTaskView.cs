// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

public class ScheduledTaskView
{
    public Settable<string> Description { get; set; }

    public Settable<string> CronExpression { get; set; }

    public Settable<DateTime> StartTime { get; set; }

    public Settable<DateTime?> EndTime { get; set; }

    public Settable<string> AgentPrompt { get; set; }

    public Settable<string> Agent { get; set; }

    public Settable<string> ThreadId { get; set; }

    public Settable<string> CreatedBy { get; set; }

    public Settable<ScheduledTaskStatus> Status { get; set; }

    public Settable<Dictionary<string, object>> ExecutionContext { get; set; }

    public Settable<int?> MaxExecutions { get; set; }

    public Settable<string> NotificationChannel { get; set; }

    public static ApiResponseEnvelope<ScheduledTaskView> CreateApiResponseEnvelope(ScheduledTaskDocument scheduledTaskDoc)
    {
        var scheduledTaskView = new ScheduledTaskView
        {
            Description = scheduledTaskDoc.Description,
            CronExpression = scheduledTaskDoc.CronExpression,
            StartTime = scheduledTaskDoc.StartTime,
            EndTime = scheduledTaskDoc.EndTime,
            AgentPrompt = scheduledTaskDoc.AgentPrompt,
            Agent = scheduledTaskDoc.Agent,
            ThreadId = scheduledTaskDoc.ThreadId,
            CreatedBy = scheduledTaskDoc.CreatedBy,
            Status = scheduledTaskDoc.Status,
            ExecutionContext = scheduledTaskDoc.ExecutionContext,
            MaxExecutions = scheduledTaskDoc.MaxExecutions,
            NotificationChannel = scheduledTaskDoc.NotificationChannel
        };

        ApiResponseEnvelope<ScheduledTaskView> apiResponse = new()
        {
            Name = scheduledTaskDoc.Name,
            Type = scheduledTaskDoc.DocumentType,
            Tags = null,
            Properties = scheduledTaskView,
        };

        return apiResponse;
    }

    public static ScheduledTaskDocument CreateModel(
        ApiRequestEnvelope<ScheduledTaskView> envelope,
        ScheduledTaskDocument? baseModel = null)
    {
        // Start with baseModel or create new document with defaults
        string id = baseModel?.Id ?? envelope.Name.Value?.ToLowerInvariant() ?? string.Empty;
        string name = baseModel?.Name ?? string.Empty;
        string description = baseModel?.Description ?? string.Empty;
        string cronExpression = baseModel?.CronExpression ?? string.Empty;
        DateTime startTime = baseModel?.StartTime ?? DateTime.UtcNow;
        DateTime? endTime = baseModel?.EndTime;
        string agentPrompt = baseModel?.AgentPrompt ?? string.Empty;
        string? agent = baseModel?.Agent;
        string? threadId = baseModel?.ThreadId;
        string createdBy = baseModel?.CreatedBy ?? "api";
        DateTime createdAt = baseModel?.CreatedAt ?? DateTime.UtcNow;
        DateTime? lastExecutionTime = baseModel?.LastExecutionTime;
        ScheduledTaskStatus status = baseModel?.Status ?? ScheduledTaskStatus.Active;
        Dictionary<string, object>? executionContext = baseModel?.ExecutionContext;
        List<ScheduledTaskExecution>? executionHistory = baseModel?.ExecutionHistory;
        int? maxExecutions = baseModel?.MaxExecutions;
        int executionCount = baseModel?.ExecutionCount ?? 0;
        string? notificationChannel = baseModel?.NotificationChannel;

        // Apply updates from envelope
        envelope.Name.ApplyTo(value =>
        {
            name = value ?? string.Empty;
            id = value?.ToLowerInvariant() ?? id;
        });

        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null)
            {
                return;
            }

            properties.Description.ApplyTo(value => description = value ?? string.Empty);
            properties.CronExpression.ApplyTo(value => cronExpression = value ?? string.Empty);
            properties.StartTime.ApplyTo(value => startTime = value);
            properties.EndTime.ApplyTo(value => endTime = value);
            properties.AgentPrompt.ApplyTo(value => agentPrompt = value ?? string.Empty);
            properties.Agent.ApplyTo(value => agent = value);
            properties.ThreadId.ApplyTo(value => threadId = value);
            properties.CreatedBy.ApplyTo(value => createdBy = value ?? "api");
            properties.Status.ApplyTo(value => status = value);
            properties.ExecutionContext.ApplyTo(value => executionContext = value);
            properties.MaxExecutions.ApplyTo(value => maxExecutions = value);
            properties.NotificationChannel.ApplyTo(value => notificationChannel = value);
        });

        return new ScheduledTaskDocument(
            Id: id,
            Name: name,
            Description: description,
            CronExpression: cronExpression,
            StartTime: startTime,
            EndTime: endTime,
            AgentPrompt: agentPrompt,
            Agent: agent,
            ThreadId: threadId,
            CreatedBy: createdBy,
            CreatedAt: createdAt,
            LastExecutionTime: lastExecutionTime,
            Status: status,
            ExecutionContext: executionContext,
            ExecutionHistory: executionHistory,
            MaxExecutions: maxExecutions,
            ExecutionCount: executionCount,
            NotificationChannel: notificationChannel
        );
    }
}
