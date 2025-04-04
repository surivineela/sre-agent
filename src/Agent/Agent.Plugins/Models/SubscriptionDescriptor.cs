// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Plugins
{
    [Description("The id and display name of an Azure subscription")]
    public sealed record SubscriptionDescriptor(
        string Id,
        string DisplayName);
}

