// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;

namespace Agent.Runtime.Helpers;

/// <summary>
/// Wrapper to expose <see cref="FirstPartyHelper"/> through dependency injection.
/// </summary>
public class FirstPartyTenantProvider : IFirstPartyTenantProvider
{
    public bool IsFirstPartyTenant()
    {
        return FirstPartyHelper.IsFirstPartyTenant();
    }
}
