// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.TlsBestPractices;

public sealed record TlsBestPracticesAgentInput(
    TlsBestPracticesInput Input,
    IReadOnlyList<string> ToolSignatures,
    ThreadContext Context);

