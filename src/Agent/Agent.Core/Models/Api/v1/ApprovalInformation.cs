// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;
public record ApprovalInformation(
    List<Guid> PendingApprovals)
{
    public bool HasPendingApprovals => PendingApprovals != null && PendingApprovals.Count > 0;
}
