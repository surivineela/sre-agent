// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.ApiResources;

public sealed record ErrorAdditionalInfo(
    string Type,
    object Info);
