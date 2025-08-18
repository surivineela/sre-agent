// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Agent.Logging;

public static class TraceAttribute
{
    // common
    public const string ThreadId = "thread.id";
    public const string AgentName = "agent.name";
    public const string OperationName = "operation.name";
    public const string FeatureConfig = "config.features";
    public const string SpanPurpose = "span.purpose";

    // user message
    public const string MessageContent = "message.content";

    // handoff
    public const string HandoffAgentName = "handoff.agent.name";
    public const string HandoffReasoning = "handoff.reasoning";

    // tool
    public const string ToolName = "tool.name";
    public const string ToolInput = "tool.input";
    public const string ToolOutput = "tool.output";
    public const string ToolDescription = "tool.description";

    // model.generation
    public const string ModelInput = "model.input";
    public const string ModelOutput = "model.output";
    public const string ModelInputTokensCount = "model.input.tokens.count";
    public const string ModelOutputTokensCount = "model.output.tokens.count";
    public const string ModelTotalTokensCount = "model.total.tokens.count";
    public const string ModelTemperature = "model.temperature";
    public const string ModelId = "model.id";

    // reasoning.loop
    public const string TriggeredBy = "triggered.by";
    public const string TriggeredMessage = "triggered.message";

    // user approval
    public const string ApprovalDescription = "approval.description";
    public const string ApprovalStatus = "approval.status";

    // incident handling
    public const string IncidentId = "incident.id";

    public const string IncidentSource = "incident.source";
    public const string IncidentMessage = "incident.message";

    // agent task
    public const string AgentTaskType = "task.type";
    public const string AgentTaskInput = "task.input";
    public const string AgentTaskStep = "task.step";
}

public static class TraceOperationName
{
    // reasoning loop
    public const string ReasoningLoop = "reasoning.loop";
    public const string UserMessage = "user.message";
    public const string UserApproval = "user.approval";
    public const string UserContinueTool = "user.continue.tool";

    // agent framework
    public const string InvokeAgent = "invoke.agent";

    public const string Tool = "tool";

    public const string Handoff = "handoff";

    public const string ModelGeneration = "model.generation";

    public const string Critic = "critic";

    public const string Summarizer = "summarizer";

    // incident handler
    public const string IncidentCreateThread = "incident.create.thread";
    public const string IncidentProcessMessage = "incident.process.message";

    // agent task
    public const string AgentTask = "task";
    public const string AgentTaskStep = "task.step";
}

/// <summary>
/// Utility class for formatting chat messages for logging and tracing purposes.
/// </summary>
public static class ChatMessageFormatter
{
    private static readonly JsonSerializerOptions _chatMessageJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Formats a collection of chat messages as JSON for logging and tracing purposes.
    /// </summary>
    /// <param name="messages">The collection of chat messages to format</param>
    /// <returns>A formatted JSON string representing the messages, or an error message if formatting fails</returns>
    public static string FormatChatMessages(IEnumerable<ChatMessage> messages)
    {
        if (messages == null || !messages.Any())
        {
            return string.Empty;
        }

        try
        {
            var formattedMessages = messages.Select(message => new
            {
                Role = message.Role.ToString(),
                Contents = message.Contents?.Select(content => new
                {
                    Type = content.GetType().Name,
                    Value = content
                })
            }).ToList();

            return JsonSerializer.Serialize(formattedMessages, _chatMessageJsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error formatting messages: {ex.Message}\n" +
                   string.Join("\n", messages.Select(m => $"{m.Role}: {m.Text?[..50]}..."));
        }
    }
}

