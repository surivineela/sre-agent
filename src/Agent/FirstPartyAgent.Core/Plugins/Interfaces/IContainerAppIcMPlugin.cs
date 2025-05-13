// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IContainerAppIcMPlugin : IIcmPlugin
{
    Task<string> GetInitialInvestigationReportAsync(string incidentId);
}
