// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.ContainerAppsRemediation;

public sealed record ContainerAppsRemediationAgentInput(
    string Input,
    IReadOnlyList<string> ToolSignatures,
    Guid ThreadId);

