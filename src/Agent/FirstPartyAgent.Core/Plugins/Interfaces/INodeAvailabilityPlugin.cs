// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface INodeAvailabilityPlugin
{
    Task<string> GetNodeAvailabilityFailures(string region, DateTime fromDate, DateTime toDate, string containerAppName, string revisionName);
}
