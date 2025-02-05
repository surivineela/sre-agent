// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

public sealed record ApprovalDescriptor(
    string ResourceId,
    string OperationName);
