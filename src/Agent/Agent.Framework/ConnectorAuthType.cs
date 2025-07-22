// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Reasoning.Models;

namespace Agent.Runtime.Reasoning.Models
{
    public enum ConnectorAuthType
    {
        None,
        ManagedIdentity,
        UAMI,
        App,
        User, // for testing
    }
}
