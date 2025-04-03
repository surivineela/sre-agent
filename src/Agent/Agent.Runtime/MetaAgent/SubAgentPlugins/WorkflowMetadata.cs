// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.MetaAgent;

public sealed record WorkflowMetadata<TInput>(
    // TODO: this should have a property of Teams thread id
    string WorkflowInstanceId,
    TInput Input);

