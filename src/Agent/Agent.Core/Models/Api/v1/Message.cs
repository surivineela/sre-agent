// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Core.Models.Api.v1;

public record AgentTaskInfo(
    Guid Id,
    string Title,
    AgentTaskStatus Status,
    DateTime? lastModified,
    string? Phase = null,
    string? StatusMessage = null
);

public record TodoInfo(
    Guid Id,
    string Title,
    TodoPlanStatus Status,
    DateTime? LastModified,
    Guid TriggerMessageId
);

public record Message(
    Guid Id,
    DateTime TimeStamp,
    Author Author,
    string Text,
    bool IsImageContent = false,
    Posted? Posted = null,
    Approval? Approval = null,
    AzCliExecution? AzCliExecution = null,
    KubectlExecution? KubectlExecution = null,
    PsqlExecution? PsqlExecution = null,
    // e.g. If this message belongs to a PagerDuty incident thread and is a discussion(called note in PagerDuty),
    // it is the PagerDuty note id. PagerDuty note id is is not a guid
    string? IncidentDiscussionId = null,
    bool IsDailyReport = false,
    // Agent Task information associated with this message (for deep investigation)
    AgentTaskInfo? AgentTaskInfo = null,
    // Memory search results from agent memory plugin
    MemorySearchResult? MemorySearchResult = null,
    // Knowledge graph search results from knowledge graph plugin
    KnowledgeGraphSearchResult? KnowledgeGraphSearchResult = null,
    // Grep search results from Workspace tools plugin
    GrepSearchResult? GrepSearchResult = null,
    // File read results from Workspace tools plugin
    ReadFileResult? ReadFileResult = null,
    // Terminal execution results from Workspace tools plugin
    TerminalExecutionResult? TerminalResult = null,
    // User question requiring interactive response (similar to Claude Code's AskUserQuestion)
    UserQuestion? UserQuestion = null,
    // Todo Plan information associated with this message (for todo plan notifications)
    TodoInfo? TodoInfo = null,
    // Indicates if the message is complete (e.g., streaming is finished)
    bool IsComplete = true,
    StreamMessageType? MessageType = null
);

public record Posted(
    bool Teams
);

public record Attachment(
    string Url,
    string Name,
    string Typep
);

