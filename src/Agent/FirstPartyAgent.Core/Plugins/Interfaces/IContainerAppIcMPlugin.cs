// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IContainerAppIcMPlugin : IIcmPlugin
{
    (DateTime StartDate, DateTime EndDate) GetIssueInvestigationTimeRange(DateTime? issueFirstOccurence, DateTime? issueLastOccurene, DateTime? reportedIssueObservedOnTime);
    Task<string> GetInitialInvestigationReportAsync(string incidentId);
    Task WasAgentHelpfulInDebuggingIssueAsync(string incidentId, bool? wasHelpful, bool? isResolutionCorrect);
}
