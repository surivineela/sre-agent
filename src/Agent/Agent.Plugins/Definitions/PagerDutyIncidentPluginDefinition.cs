// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class PagerDutyIncidentPluginDefinition(IPagerDutyIncidentPlugin incidentPlugin)
    {
        [AgentTool(ToolMode.Auto)]
        [Description("Gets latest PagerDuty incidents related to an Azure resource.")]
        public async Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentsAsync(
            [Description("Azure resource id")] string resourceId,
            [Description("max number of incidents to return")] uint maxResults = 5)
        {
            return await incidentPlugin.GetPagerDutyIncidentsAsync(resourceId, maxResults);
        }

        [AgentTool(ToolMode.Auto)]
        [Description("Resolves a PagerDuty incident")]
        public async Task<string> ResolvePagerDutyIncidentAsync([Description("PagerDuty incident id")] string incidentId)
        {
            await incidentPlugin.ResolvePagerDutyIncidentAsync(incidentId);
            return $"Successfully resolved PagerDutyIncident {incidentId}";
        }

        [Description("Acknowledges a PagerDuty incident")]
        public async Task<string> AcknowledgePagerDutyIncidentAsync([Description("PagerDuty incident id")] string incidentId)
        {
            await incidentPlugin.AcknowledgePagerDutyIncidentAsync(incidentId);
            return $"Successfully acknowledged PagerDutyIncident {incidentId}";
        }

        [Description("Add note to a PagerDuty Incident")]
        public async Task<string> AddNoteToPagerDutyIncident(
            [Description("PagerDuty incident id")] string incidentId,
            [Description("Note to add to the incident")] string note)
        {
            return await incidentPlugin.AddNoteToIncident(incidentId, note);
        }

        [AgentTool(ToolMode.Auto)]
        [Description("Closes an Azure Monitor alert thread by marking it as closed. This can be used to close an alert thread that is no longer active.")]
        public async Task CloseAzureMonitorAlert([Description("The GUID for the Azure alert. May contain the full resource path (/subscriptions/.../alertId) but only the last part (alertId) is needed")]string alertId)
        {
            await incidentPlugin.CloseAzureMonitorAlert(alertId);
        }
    }
}
