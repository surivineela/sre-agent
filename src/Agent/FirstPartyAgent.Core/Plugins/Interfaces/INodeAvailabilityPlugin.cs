// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.ComponentModel;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Interfaces;

// [MANDATORY]
public interface INodeAvailabilityPlugin
{
    Task<string> GetNodeAvailabilityFailures(string region, DateTime fromDate, DateTime toDate, string containerAppName, string revisionName);
}
