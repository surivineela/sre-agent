// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record Message(
    Guid Id,
    DateTime TimeStamp,
    Author Author,
    string Text,
    bool IsImageContent = false,
    Posted? Posted = null,
    Approval? Approval = null,
    // e.g. If this message belongs to a PagerDuty incident thread and is a discussion(called note in PagerDuty), 
    // it is the PagerDuty note id. PagerDuty note id is is not a guid
    string? IncidentDiscussionId = null, 
    bool IsDailyReport = false
);

public record Posted(
    bool Teams
);

public record Attachment(
    string Url,
    string Name,
    string Typep
);
