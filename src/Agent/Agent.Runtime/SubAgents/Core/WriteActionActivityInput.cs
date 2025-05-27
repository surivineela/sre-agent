// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

public class WriteActionActivityInput
{
    public string ThreadId { get; set; } = string.Empty;

    public FunctionCallContent? FunctionCall { get; set; }

    public IReadOnlyList<string> ToolSignatures { get; set; } = new List<string>();

    public Guid ActionId { get; set; }

    public string OrchestrationId { get; set; } = string.Empty;
}
