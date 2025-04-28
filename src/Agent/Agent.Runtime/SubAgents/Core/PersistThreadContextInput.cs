// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

public class PersistThreadContextInput
{
    public ThreadContext? ThreadContext { get; set; }
    public string OrchestrationInstanceId { get; set; } = string.Empty;
    public int StepCounter { get; set; } = 0;
    public Guid ThreadId { get; set; }
    public ReasoningState ReasoningState { get; set; } = ReasoningState.Undefined;
    public string StateMessage { get; set; } = string.Empty;
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
}



