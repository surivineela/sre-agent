// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Core.Attributes;
using Agent.Core.Models;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(
        Category = ToolCategories.IncidentManagement,
        IsIncidentHandlerPlugin = true,
        IncidentPlatform = Core.Configuration.IncidentManagementType.PagerDuty)]
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
        [Description("Gets a specific PagerDuty incident by incident ID.")]
        public async Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentById(
            [Description("PagerDuty incident id")] string incidentId)
        {
            return await incidentPlugin.GetPagerDutyIncidentById(incidentId);
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

        [AgentTool(ToolMode.Auto)]
        [Description("Queries the PagerDuty AI chat assistant with incident-specific context to get intelligent insights, troubleshooting steps, runbook generation, or diagnostic recommendations for a specific PagerDuty incident.")]
        public async Task<string> QueryPagerDutyIncidentChatAsync(
            [Description("The specific question or request to ask about the incident (e.g., 'Generate a runbook for this incident', 'What are the potential root causes?', 'Provide step-by-step troubleshooting guidance', 'Suggest mitigation steps')")] string userQuery,
            [Description("The PagerDuty incident ID to query about (e.g., 'Q391Y5VW0YYUEL'). The chat assistant will use this incident's context to provide relevant answers.")] string incidentId)
        {
            if (string.IsNullOrEmpty(incidentId))
                throw new ArgumentException("Incident ID must be provided.");
            return await incidentPlugin.GetAgentResponseAsync(userQuery, incidentId);
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
