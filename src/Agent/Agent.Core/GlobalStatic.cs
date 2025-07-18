// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Models;

namespace Agent.Core;

// TODO: figure out how to DI these into DiagnosePlugin
public static class GlobalStatic
{
    public static ConcurrentDictionary<ApprovalDescriptor, ApprovalStatus> ApprovalStatus { get; } = new();
}
