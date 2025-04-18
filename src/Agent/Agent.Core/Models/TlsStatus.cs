// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public sealed record TlsStatus(
    [Description("Resource id of the app")] string ResourceId,
    [Description("Name of the app")] string Name,
    [Description("Azure location of the app")] string Location,
    [Description("Current minimum TLS version. Optional. Valid values: 1.0, 1.1, 1.2, 1.3")] string? MinimumTlsVersion);
