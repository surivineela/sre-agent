// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models;
using System.Collections.Concurrent;

namespace Agent.Core;

// TODO: figure out how to DI these into DiagnosePlugin
public static class GlobalStatic
{
    public static TeamsConnector TeamsConnector;

    public static ConcurrentDictionary<ApprovalDescriptor, ApprovalStatus> ApprovalStatus { get; } = new();
}
