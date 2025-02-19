// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models.ICM;
using FirstPartyAgent.Constants;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins.Definitions
{
    public class IcmPluginDefinition(IIcmPlugin plugin)
    {
        private readonly IIcmPlugin _plugin = plugin;

        [KernelFunction(KernelFunctionNames.Icm.IcmGetIncidentInfo)]
        [Description("Get ICM incident information")]
        public async Task<Incident?> GetIncidentInfo(
            [Description("Incident ID")] string incidentId)
        {
            return await _plugin.GetIncidentInfo(incidentId);
        }

        [KernelFunction(KernelFunctionNames.Icm.IcmGetIncidentsByTeam)]
        [Description("Gets a list of ICM incidents by Tenant and Team")]
        public async Task<List<Incident>> GetIncidents(
            [Description("The name of the tenant")] string tenant,
            [Description("Comma-separated list of metrics to include")] string metrics)
        {
            return await _plugin.GetIncidents(tenant, metrics);
        }

        [KernelFunction(KernelFunctionNames.Icm.IcmMitigateIncident)]
        [Description(@"Mitigate an IcM incident
This operation will set the given IcM Incident to Mitigated state. And you must give a reason of this mitigation action.

Input parameters:
- incidentId: The Id of the IcM incident. It is usually a integer number.
- reason: The additional information for this mitigation action. Usually it is a reason why you can mitigate this incident.

The operation will mark the given incident as mitigated.
The return value is a boolean value for indicating if the operation is successful.
")]
        public async Task<bool> MitigateIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("The comment for mitigation action")] string reason)
        {
            return await _plugin.MitigateIncident(incidentId, reason);
        }

        [KernelFunction(KernelFunctionNames.Icm.IcmResolveIncident)]
        [Description("Resolve an ICM incident")]
        public async Task<bool> ResolveIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("comment/reason for resolution action")] string reason)
        {
            return await _plugin.ResolveIncident(incidentId, reason);
        }

        [KernelFunction(KernelFunctionNames.Icm.IcmAddTag)]
        [Description("Add a tag to an ICM incident")]
        public async Task<bool> AddTag(
            [Description("Id of the incident")] string incidentId,
            [Description("Tag to add")] string tag)
        {
            return await _plugin.AddTag(incidentId, tag);
        }

        [KernelFunction(KernelFunctionNames.Icm.IcmGetDisscussionEntries)]
        [Description(@"Get ICM discussion entries
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

        [KernelFunction(KernelFunctionNames.Icm.IcmAddDiscussionEntry)]
        [Description(@"Add a discussion entry to an ICM incident
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
