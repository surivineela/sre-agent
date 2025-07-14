// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Kusto
{
    public enum KustoAuthenticationType
    {
        ManagedIdentity,
        UAMI,
        App,
        User, // for testing
    }
}
