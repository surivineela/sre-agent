// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.HelperAgents;

namespace Agent.Runtime.SubAgents.KubernetesAgent;

public sealed record KubernetesAgentInput(
    string Input,
    IReadOnlyList<string> ToolSignatures,
    Guid ThreadId,
    IReadOnlyList<HelperAgentInput> HelperAgentsInputs);

