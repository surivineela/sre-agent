// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Plugins
{
    public sealed record AppServiceDescriptor(
        string ResourceId,
        string Name,
        [Description("app means WebApp, functionapp means FunctionApp")]
            string Kind,
        string Location,
        string Sku,
        string State,
        string ResourceGroup);
}

