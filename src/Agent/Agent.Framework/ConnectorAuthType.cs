// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;

namespace Agent.Framework.Reasoning.Models
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
