// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework
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
