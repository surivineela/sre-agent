// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using FirstPartyAgent.Core.Plugins.Interfaces;
using OpenAI.Chat;

namespace FirstPartyAgent.Plugins
{
    public class IcmPlugin : IIcmPlugin
    {
        private readonly IConfiguration _config;
        private readonly ICMWorkflowClient _icmAutomationClient;
        private readonly ILogger<IcmPlugin> _logger;
        public IChatClient ChatClient;

        private readonly string summarizationPrompt = """
            You will be provided with the ICM details in a json format. You need to extract the required information from the `summary` and format it in markdown format as mentioned below.
            Any information that is there in ICM must be returned in the returned response. If there is any information that doesn't suit well in any of the categories, place it in `Additional Information` field. But DO NOT leave any information out.

            *** Response Template:**
            Response MUST BE in a markdown file with the following format:
            ```markdown
            # ICM Details
            `<Provide IncidentId and title of ICM here>`

            ## Customer Details
            - **Subscription:** `<This should be given as Subscription Id>`
            - **Tenant Id:** `<This should be given as Tenant Id>`
            ## Container App Resource
            - **"ContainerAppName":** `<This should be given in Resource Uri. A sample URI of ContainerApp Resource is like this: https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/<SubscriptionID>/resourceGroups/<ResourceGroupName>/providers/Microsoft.App/containerApps/<ContainerAppName>``,
            - **""ResourceGroup":** `"<Extract this from ResourceUri mentioned above>"`
            - **"ManagedEnvironmentName"**: `"Find if Managed Environment Name/URI is given somewhere in summary. The managed Environment Resource URI looks like this: `https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/<SubscriptionID`/resourceGroups/<ResourceGroup>/providers/Microsoft.App/managedEnvironments/<ManagedEnvironmentName>`    "Problem": "<Analyse summary to understand the problem customer is facing and summarize here in concise words",

            ## Customer Impact
            `<Analyse Summary to understand the impact customer is having e.g. production is down etc.`

            ## Impacted Product Area
            `<Analyse summary to understand what product area customer is facing issue with? e.g. ContainerApp, ContainerApp Job, Sessions etc.`

            ## Timeline of Issue
            `<Analyse summary to find when the issue occurred>`
            > Note: If it is not provided or not clear than clearly highlight the gap and ask for manual inputs. 

            ## Investigations Done
            `<List any investigations done by the customer or any other agent. Any kusto queries provided should be included here.>`

            ## Additional Information
            `<Any additional information provided in the summary.>`
            """;
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
        public async Task<Incident?> GetIncidentInfo(
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
        public async Task<List<DiscussionEntry>?> GetDiscussionEntries(
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

        public async Task<string> SummarizeICM(string incidentId)
        {
            var incident = await GetIncidentInfo(incidentId);
            if (incident == null)
            {
                return "Incident not found.";
            }

            var messages = new List<Microsoft.Extensions.AI.ChatMessage>();
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, summarizationPrompt));

            var serializationOptions = new JsonSerializerOptions
            {
                WriteIndented = true // Makes the JSON output more readable
            };

            string incidentInfo =  JsonSerializer.Serialize(incident, serializationOptions);
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, incidentInfo));

            var options = new ChatOptions
            {
                Temperature = (float)0.2,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            var response = await ChatClient.GetResponseAsync(messages, options);
            string summary = response.Text;

            _logger.LogInformation($"ICM summary for ICM ID {incidentId}: {summary}");
            return summary;
        }
    }
}
