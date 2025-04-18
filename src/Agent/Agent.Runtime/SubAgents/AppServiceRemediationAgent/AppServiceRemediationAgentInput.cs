// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.AppServiceRemediation;


public sealed record AppServiceRemediationAgentInput(
    AppServiceRemediationInput Input,
    IReadOnlyList<string> ToolSignatures,
    Guid ThreadId);

