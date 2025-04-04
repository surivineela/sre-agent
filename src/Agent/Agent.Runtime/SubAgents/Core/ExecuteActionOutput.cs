// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

public sealed record ExecuteActionOutput(
    ChatMessage ChatMessage,
    bool Is202Submit);

