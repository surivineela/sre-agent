// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public class TlsBestPracticesInput
{
    [Description("Desired TLS Version. eg: 1.2 which apps should be migrated to")]
    public string DesiredVersion { get; set; }

    [Description("Apps which are not on DesiredTLSVersion")]
    public List<TlsStatus> AppsInViolation { get; set; }

    [Description("Detailed description of the issue.")]
    public string message { get; set; }
}

