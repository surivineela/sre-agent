// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using FirstPartyAgent.Core.Plugins.Interfaces;

namespace FirstPartyAgent.Plugins
{
    public class IcmPlugin : IIcmPlugin
    {
        private readonly IConfiguration _config;
        internal readonly ICMWorkflowClient _icmAutomationClient;
        private readonly ILogger<IcmPlugin> _logger;
        public IChatClient ChatClient;

        public IcmPlugin(
            IConfiguration config,
            ICMWorkflowClient icmAutomationClient,
            IChatClient chatClient,
            ILogger<IcmPlugin> logger)
        {
            _config = config;
            _icmAutomationClient = icmAutomationClient;
            ChatClient = chatClient;
            _logger = logger;
        }

        [KernelFunction("get_icm_incident_info")]
        [Description("Get ICM incident information")]
        public virtual async Task<Incident?> GetIncidentInfo(
           [Description("Incident ID")] string incidentId)
        {
            return await _icmAutomationClient.GetIncidentAsync(incidentId);
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
            return await _icmAutomationClient.MitigateIncidentAsync(incidentId, reason) == "Success";
        }

        [KernelFunction("icm_resolve_incident")]
        [Description("Resolve an ICM incident")]
        public async Task<bool> ResolveIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("comment/reason for resolution action")] string reason)
        {
            return await _icmAutomationClient.ResolveIncidentAsync(incidentId, reason) == "Success";
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
        public virtual async Task<List<DiscussionEntry>?> GetDiscussionEntries(
           [Description("Incident ID")] string incidentId,
           [Description("From time of the query")] DateTimeOffset queryFrom)
        {
            return await _icmAutomationClient.GetIncidentDiscussionEntriesAsync(incidentId, queryFrom);
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
            return await _icmAutomationClient.PostDiscussionEntryAsync(incidentId, text) == "Success";
        }

        public async Task<bool> AddTag(
            [Description("Incident ID")] string incidentId,
            [Description("Tag to add")] string tag)
        {
            return await _icmAutomationClient.AddTagToIncident(incidentId, tag) == "Success";
        }
    }
}
