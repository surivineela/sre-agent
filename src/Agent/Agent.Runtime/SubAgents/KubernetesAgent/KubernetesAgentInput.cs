// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.KubernetesAgent;

public sealed record KubernetesAgentInput(
    string Input,
    IReadOnlyList<string> ToolSignatures,
    ThreadContext Context);

