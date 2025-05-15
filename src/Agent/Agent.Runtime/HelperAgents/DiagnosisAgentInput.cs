// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Runtime.HelperAgents;

public class DiagnosisAgentInput : HelperAgentInput
{
    [JsonIgnore]
    public override Type AgentType => typeof(DiagnosisAgent);

    public string CustomInstructions { get; set; } = string.Empty;
}
