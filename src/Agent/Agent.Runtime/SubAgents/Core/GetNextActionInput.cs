// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

public class GetNextActionInput
{
    public required List<ChatMessage> ChatMessages { get; set; }
    public int StepCounter { get; set; }
    public required IReadOnlyList<string> ToolSignatures { get; set; }
}
