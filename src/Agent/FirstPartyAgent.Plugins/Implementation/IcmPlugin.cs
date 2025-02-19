// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Models.ICM;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public class IcmPlugin : IIcmPlugin
    {
        private readonly IConfiguration _config;
        private readonly IcmAutomationClient _icmAutomationClient;

        public IcmPlugin(IConfiguration config, IcmAutomationClient icmAutomationClient)
        {
            _config = config;
            _icmAutomationClient = icmAutomationClient;
        }

        [KernelFunction("get_icm_incident_info")]
        [Description("Get ICM incident information")]
        public async Task<Incident?> GetIncidentInfo(
           [Description("Incident ID")] string incidentId)
        {
            string workflowName = _config.GetValue("ICM:WorkflowNames:FetchICMIncidentInfo", string.Empty);
            if (string.IsNullOrEmpty(workflowName))
            {
                throw new Exception("ICM:WorkflowNames:FetchICMIncidentInfo is not set.");
            }
            Dictionary<string, string> body = new()
            {
                { "incidentId", incidentId }
            };
            var (success, incident) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<Incident>(workflowName, body);
            if (success)
            {
                return incident;
            }
            else
            {
                throw new Exception($"Failed to fetch incident info for incidentId: {incidentId}");
            }
        }

        [KernelFunction("get_icm_incidents_by_team")]
        [Description("Gets a list of ICM incidents by Tenant and Team")]
        public async Task<List<Incident>> GetIncidents(
        [Description("The name of the tenant")] string tenant,
        [Description("Comma-separated list of metrics to include")] string metrics)
        {
            return new List<Incident>();
        }

        [KernelFunction("icm_mitigate_incident")]
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
            const string workflowName = "Workflow-IcM-MitigateIncident";

            Dictionary<string, string> body = new()
            {
                { "IncidentId", incidentId },
                { "Message", reason }
            };
            var (success, _) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<object>(workflowName, body);
            return success;
        }

        [KernelFunction("icm_resolve_incident")]
        [Description("Resolve an ICM incident")]
        public async Task<bool> ResolveIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("comment/reason for resolution action")] string reason)
        {

            string workflowName = _config.GetValue("ICM:WorkflowNames:ResolveIncident", string.Empty);
            if (string.IsNullOrEmpty(workflowName))
            {
                throw new Exception("ICM:WorkflowNames:ResolveIncident is not set.");
            }
            Dictionary<string, string> body = new()
            {
                { "incidentId", incidentId },
                { "message", reason }
            };
            var (success, _) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<object>(workflowName, body);
            return success;
        }

        [KernelFunction("icm_add_tag")]
        [Description("Add a tag to an ICM incident")]
        public async Task<bool> AddTag(
            [Description("Id of the incident")] string incidentId,
            [Description("Tag to add")] string tag)
        {
            string workflowName = _config.GetValue("ICM:WorkflowNames:AddTag", string.Empty);
            if (string.IsNullOrEmpty(workflowName))
            {
                throw new Exception("ICM:WorkflowNames:AddTag is not set.");
            }
            Dictionary<string, string> body = new()
            {
                { "incidentId", incidentId },
                { "tag", tag }
            };
            var (success, _) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<object>(workflowName, body, "ManualTrigger");
            return success;
        }

        [KernelFunction("icm_get_discussion_entries")]
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
            const string workflowName = "Workflow-IcM-GetDiscussions";

            Dictionary<string, string> body = new()
            {
                { "IncidentId", incidentId },
                { "QueryFrom", queryFrom.ToString("s", System.Globalization.CultureInfo.InvariantCulture) }
            };
            var (success, discussionEntries) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<List<DiscussionEntry>>(workflowName, body);
            if (success)
            {
                return discussionEntries;
            }
            else
            {
                Console.WriteLine($"Failed to fetch discussion entries for incidentId: {incidentId}");
                return null;
            }
        }

        [KernelFunction("icm_add_discussion_entry")]
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
            const string workflowName = "Workflow-IcM-AddDiscussion";
            Dictionary<string, string> body = new()
            {
                { "incidentId", incidentId },
                { "text", text }
            };
            var (success, _) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<object>(workflowName, body);
            return success;
        }
    }
}
