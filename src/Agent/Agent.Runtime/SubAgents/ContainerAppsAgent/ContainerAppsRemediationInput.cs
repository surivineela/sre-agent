// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.SubAgents.ContainerAppsRemediation;

public sealed record ContainerAppsRemediationInput(
    [Description("Detailed description of the issue with full azure resource id of the Azure Container Apps resources that we need help with. Should restart with /subscriptions/<>....")]
    string message);

