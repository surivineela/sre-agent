// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Models.Api.v1
{
    public enum ThreadSource
    {
        Portal,        // Legacy type for portal chat conversations (keeping for backward compatibility)
        Conversation,  // New default type for regular chat conversations
        Agent,         // Agent proactively created thread, e.g. daily report
        Teams,         // Agent tagged in teams channel, chat group or direct message
        Alert,         // Agent invoked by alert or IcM webhook
        Incident,       // For incident/security related threads
        WelcomeMessage,
    }

    public enum IncidentType
    {
        PagerDuty,
        Icm,
        AzMonitor,
        ServiceNow
    }

    public enum ThreadType
    {
        Prod,
        Test  // For testing purposes, agent will run in Readonly mode
    }

    public sealed record IncidentSource(
        IncidentType IncidentType,
        string IncidentId);

    //Theread type: PROD/TEST
    public record Thread(
        Guid Id,
        string Title,
        Message? StartMessage,
        Message? LastMessage,
        DateTime CreatedTimestamp,
        DateTime ModifiedTimestamp,
        FeatureConfigModel? FeatureConfig,
        ThreadSource Source = ThreadSource.Conversation,
        string? WaitReason = null,
        DateTime? WaitUntil = null,
        IncidentSource? IncidentSource = null,
        ThreadType? Type = ThreadType.Prod
    )
    {
        public Status? Status { get; set; } = null;
        public DateTime? LastReadTime { get; set; } = null;
        public DateTime EvaluatedTimestamp { get; set; } = default;
        public DateTime TrajectoryGeneratedTimestamp { get; set; } = default;
        public string? AgentMode { get; set; } = null;
    };

    public class Status
    {
        public ActionsStatus? ActionsStatus { get; set; } = null;
        public IncidentStatus? IncidentStatus { get; set; } = null;
    };

    public class ActionsStatus
    {
        public bool HasCriticalActions { get; set; } = false;
        public bool HasWarningActions { get; set; } = false;
    };

    public class IncidentStatus
    {
        public string? IncidentId { get; set; } = null;
        public string? Status { get; set; } = null;
    };

    public record CreateThreadRequest(
        [Required] CreateMessageRequest StartMessage,
        ThreadSource? Source = ThreadSource.Conversation  // New threads default to Conversation
    );

    public record CreateMessageRequest(
        [Required] string Text,
        string UserId,
        string DisplayName
    );

    public record FeedbackRequest(
        [Required] bool IsPositive,
        string? FeedbackText
    );

    public record UpdateAgentModeRequest(
        [Required] string AgentMode
    );
}

