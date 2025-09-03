// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record AgentTaskInfo(
    Guid Id,
    string Title,
    AgentTaskStatus Status,
    DateTime? lastModified
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
    // e.g. If this message belongs to a PagerDuty incident thread and is a discussion(called note in PagerDuty),
    // it is the PagerDuty note id. PagerDuty note id is is not a guid
    string? IncidentDiscussionId = null,
    bool IsDailyReport = false,
    // Agent Task information associated with this message (for deep investigation)
    AgentTaskInfo? AgentTaskInfo = null
);

public record Posted(
    bool Teams
);

public record Attachment(
    string Url,
    string Name,
    string Typep
);
