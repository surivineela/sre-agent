// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.Reasoning.Models
{
    public enum ConnectorAuthType
    {
        None,
        ManagedIdentity,
        UAMI,
        App,
        User, // for testing
        AgentSpace
    }
}
