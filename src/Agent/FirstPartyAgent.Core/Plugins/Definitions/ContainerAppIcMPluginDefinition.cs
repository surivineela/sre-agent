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
    public class ContainerAppIcMPluginDefinition : IcmPluginDefinition
    {
        private readonly IContainerAppIcMPlugin _plugin;

        public ContainerAppIcMPluginDefinition(IContainerAppIcMPlugin plugin) : base(plugin)
        {
            _plugin = plugin;
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
        public override async Task<Incident?> GetIncidentInfo(
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
        public override async Task<List<DiscussionEntry>?> GetDiscussionEntries(
           [Description("Incident ID")] string incidentId,
           [Description("From time of the query")] DateTimeOffset queryFrom)
        {
            return await _plugin.GetDiscussionEntries(incidentId, queryFrom);
        }
    }
}
