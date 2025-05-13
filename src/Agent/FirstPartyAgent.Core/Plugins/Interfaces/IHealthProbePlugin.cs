// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.ComponentModel;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Interfaces;

// [MANDATORY]
public interface IHealthProbePlugin
{
    Task<string> GetHealthProbeFailures(string region, DateTime fromDate, DateTime toDate, string containerAppName, string revisionName, SamplingOptions? samplingOptions = null);
}
