// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public class TlsBestPracticesInput
{
    [Description("Desired minimum TLS version which apps should be migrated to. Valid values: 1.2, 1.3")]
    public required string DesiredVersion { get; set; }

    [Description("Apps which are not on DesiredTLSVersion")]
    public required List<TlsStatus> AppsInViolation { get; set; }
}

