// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;
public class CheckApprovalActivityInput
{
    public IReadOnlyList<string> ToolSignatures { get; set; } = new List<string>();
    public string ThreadId { get; set; } = string.Empty;
    public string OrchestrationId { get; set; } = string.Empty;
    public FunctionCallContent? FunctionCall { get; set; }
    public Guid ActionId { get; set; } = Guid.Empty;
}
