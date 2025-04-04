// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

public class GetNextActionInput
{
    public List<ChatMessage> ChatMessages { get; set; }
    public int StepCounter { get; set; }
    public IReadOnlyList<string> ToolSignatures { get; set; }
}

