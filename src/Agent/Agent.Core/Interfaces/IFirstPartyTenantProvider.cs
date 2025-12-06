// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Interfaces
{
    /// <summary>
    /// Abstraction for determining whether the current agent is running in a first-party tenant.
    /// </summary>
    public interface IFirstPartyTenantProvider
    {
        /// <summary>
        /// Returns true when the current execution context is a first-party tenant.
        /// </summary>
        bool IsFirstPartyTenant();
    }
}
