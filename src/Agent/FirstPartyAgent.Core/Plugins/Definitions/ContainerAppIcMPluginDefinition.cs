// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Constants;
using System.ComponentModel;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.SemanticKernel;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.Plugins.Definitions
{
    /// <summary>
    /// Using this approach because SK does not allow interfaces to be used as kernel functions
    /// https://github.com/microsoft/semantic-kernel/issues/10323
    /// </summary>
    public class ContainerAppIcMPluginDefinition
    {
        private readonly IContainerAppIcMPlugin _plugin;

        public ContainerAppIcMPluginDefinition(IContainerAppIcMPlugin plugin)
        {
            _plugin = plugin;
        }

        [KernelFunction(KernelFunctionNames.ACA.GetIssueInvestigationTimeRange)]
        [Description("It calculates the effective issue investigation time range based on information available in context.")]
        public (DateTime StartDate, DateTime EndDate) GetIssueInvestigationTimeRange(
            [Description("The timestamp of the first occurrence of the issue. Skip if not available")] DateTime? issueFirstOccurence,
            [Description("The timestamp of the last occurrence of the issue. Skip if not available")] DateTime? issueLastOccurene,
            [Description("The timestamp when the issue was observed and reported. Skip if not available")] DateTime? reportedIssueObservedOnTime)
        {
            return _plugin.GetIssueInvestigationTimeRange(issueFirstOccurence, issueLastOccurene, reportedIssueObservedOnTime);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetInitialInvestigationSummaryReport)]
        [Description("Fetches the Initial Investigation Detailed Report for a given incident ID. Returns an empty string or an error message if the incident ID is invalid. **DO NOT SUMMARIZE IT'S OUTPUT**")]
        public async Task<string> GetInitialInvestigationReportAsync(
            [Description("Incident ID")] string incidentId)
        {
            return await _plugin.GetInitialInvestigationReportAsync(incidentId);
        }

        [KernelFunction(KernelFunctionNames.Icm.IcmGetIncidentInfo)]
        [Description("Get original ICM incident information.")]
        public async Task<FirstPartyAgent.Models.Incident?> GetIncidentInfo(
        [Description("Incident ID")] string incidentId)
        {
            return await _plugin.GetIncidentInfo(incidentId);

        }

        [KernelFunction(KernelFunctionNames.Icm.IcmGetDisscussionEntries)]
        [Description(@"Get original ICM discussion entries
        This operation will get all the discussion entries of the given IcM Incident.

        Input parameters:
        - IncidentId: The Id of the IcM incident. It is usually a integer number.
        - QueryFrom: The timestamp for filter the discussion entries which are created after it.

        The return value is a list of discussion entries of the given IcM Incident. Each discussion entry includes the following information:
        - IncidentId: The Id of the IcM incident.
        - TimeStamp: The timestamp of the discussion entry.
        - ChangedBy: The user who created this discussion entry.
        ")]
        public async Task<List<DiscussionEntry>?> GetDiscussionEntries(
           [Description("Incident ID")] string incidentId,
           [Description("From time of the query")] DateTimeOffset queryFrom)
        {
            return await _plugin.GetDiscussionEntries(incidentId, queryFrom);
        }


        [KernelFunction(KernelFunctionNames.ACA.SubmitAgentFeedback)]
        [Description(@"
        Submit feedback regarding the agent's assistance in debugging the issue.

        Input parameters:
        - IncidentId: The unique identifier of the incident.
        - wasHelpful: Indicates whether the agent was helpful in debugging the issue (true/false). Use null to skip this feedback.
        - isResolutionCorrect: Indicates whether the resolution provided by the agent was accurate (true/false). Use null to skip this feedback.
        ")]
        public async void WasAgentHelpfulInDebuggingIssueAsync(
           [Description("The unique identifier of the incident.")] string incidentId,
           [Description("Indicates if the agent was helpful in debugging the issue (true/false). Use null to skip.")] bool? wasHelpful,
           [Description("Indicates if the resolution provided by the agent was accurate (true/false). Use null to skip.")] bool? isResolutionCorrect)
        {
            await _plugin.WasAgentHelpfulInDebuggingIssueAsync(incidentId, wasHelpful, isResolutionCorrect);
        }

        [KernelFunction(KernelFunctionNames.Icm.IcmAddDiscussionEntry)]
        [Description(@"**Note: DO NOT CALL IT AUTOMATICALLY. ALWAYS ASK USER BEFORE CALLING IT**
        Add a discussion entry or summary of final investigate to an ICM incident
        This operation will add a discussion entry to the given IcM Incident. 

        input parameters:
        - incidentId: The Id of the IcM incident. It is usually a integer number.
        - text: The content of the discussion entry.

        The operation will add a discussion entry to the given incident.
        The return value is a boolean value for indicating if the operation is successful.
        ")]
        public async Task<bool> AddDiscussionEntry(
        [Description("Incident ID")] string incidentId,
        [Description("Discussion entry text")] string text)
        {
            return await _plugin.AddDiscussionEntry(incidentId, text);
        }
    }
}
