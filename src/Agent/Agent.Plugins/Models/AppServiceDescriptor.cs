// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Plugins.Models;

public sealed record AppServiceDescriptor(
    string ResourceId,
    string Name,
    [Description("app means WebApp, functionapp means FunctionApp")]
    string Kind,
    string Location,
    string Sku,
    string State,
    string ResourceGroup,
    int? NumberOfWorkers,
    bool? AutoHealEnabled,
    bool? AlwaysOn,
    bool? HealthCheckEnabled);
