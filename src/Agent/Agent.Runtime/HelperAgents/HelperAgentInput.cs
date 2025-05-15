// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Runtime.HelperAgents;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DiagnosisAgentInput), nameof(DiagnosisAgentInput))]
public abstract class HelperAgentInput
{
    public IReadOnlyList<string> ToolSignatures { get; set; } = [];

    [JsonIgnore]
    public abstract Type AgentType { get; }
}

