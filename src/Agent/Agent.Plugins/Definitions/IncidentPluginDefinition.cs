// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DataModels;

namespace Agent.Plugins.Definitions
{
    public class IncidentPluginDefinition(IIncidentPlugin incidentPlugin)
    {
        [Description("Gets latest PagerDuty incidents related to an Azure resource.")]
        public async Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentsAsync(
            [Description("Azure resource id")] string resourceId,
            [Description("max number of incidents to return")] uint maxResults = 5)
        {
            return await incidentPlugin.GetPagerDutyIncidentsAsync(resourceId, maxResults);
        }

        [Description("Resolves a PagerDuty incident")]
        public async Task ResolvePagerDutyIncidentAsync([Description("PagerDuty incident id")] string incidentId)
        {
            await incidentPlugin.ResolvePagerDutyIncidentAsync(incidentId);
        }
    }
}
