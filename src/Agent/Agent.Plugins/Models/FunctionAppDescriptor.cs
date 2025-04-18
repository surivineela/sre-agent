// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Plugins.Models;

public sealed record FunctionAppDescriptor(
    string ResourceId,
    string Name,
    [Description("functionapp means FunctionApp, app means App Service WebApp")]
    string Kind,
    string Location,
    string Sku,
    string State,
    string ResourceGroup,
    string? VnetId,
    string? StackVersion,
    string? PlanType,
    string ? MinTlsVersion,
    bool? WebSocketEnabled,
    int? NumberOfWorkers,
    bool? AutoHealEnabled,
    bool? AlwaysOn,
    bool? HealthCheckEnabled);

