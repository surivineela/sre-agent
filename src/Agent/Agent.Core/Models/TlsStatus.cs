// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

public sealed record TlsStatus(
    string ResourceId,
    string Name,
    string Location,
    string MinimumTlsVersion);
