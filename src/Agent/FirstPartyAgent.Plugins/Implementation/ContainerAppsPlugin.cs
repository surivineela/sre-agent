// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Helpers;
using Agent.Core.Models;
using FirstPartyAgent.Configuration;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace FirstPartyAgent.Plugins
{
    public class ContainerAppsPlugin : IContainerAppsPlugin
    {
        private readonly IcmSettings _icmSettings;
        private readonly IcmAutomationClient _icmAutomationClient;
        private readonly HttpClient _httpClient = new HttpClient();

        public ContainerAppsPlugin(IOptions<IcmSettings> icmSettings, IcmAutomationClient icmAutomationClient)
        {
            _icmSettings = icmSettings.Value;
            _icmAutomationClient = icmAutomationClient;
        }

        public async Task<SubscriptionDetail?> GetSubscriptionDetail(
     string subscriptionId)
        {
            const string workflowName = "Workflow-Data-GetSubscriptionDetail";

            Dictionary<string, string> body = new()
            {
                { "SubscriptionId", subscriptionId }
            };

            var (success, subscriptionDetail) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<SubscriptionDetail>(workflowName, body);

            if (success)
            {
                return subscriptionDetail;
            }
            else
            {
                return new SubscriptionDetail(subscriptionId);
            }
        }

        public async Task<bool> SetSubscriptionQuota(string subscriptionId, string region, string quotaType)
        {
            const string workflowName = "Workflow-GenevaAction-SetSubscriptionQuota";

            Dictionary<string, string> body = new()
            {
                { "SubscriptionId", subscriptionId },
                { "Region", region },
                { "QuotaType", quotaType },
            };
            var (success, _) = await _icmAutomationClient.TriggerIcmWorkflowWithResponse<object>(workflowName, body, "manual");
            return success;
        }

        public async Task<TeamsPostMessageResponse?> PostTeamsDiscussionAsync(string incidentId, string title, string content)
        {
            // prepend the icm link of the incident
            content = $"<p><a href=\"https://portal.microsofticm.com/imp/v5/incidents/details/{incidentId}/summary\">{incidentId}</a></p><br/>{content}";

            var body = new Dictionary<string, object>
            {
                { "IncidentId", incidentId },
                { "Title", title },
                { "Content", content }
            };

            return await SendTeamsRequestAsync(body);
        }

        public async Task<TeamsPostMessageResponse?> ReplyTeamsDiscussionAsync(string incidentId, string messageId, string content)
        {
            var body = new Dictionary<string, object>
            {
                { "IncidentId", incidentId },
                { "MessageId", messageId },
                { "Content", content }
            };
            return await SendTeamsRequestAsync(body);
        }

        private async Task<TeamsPostMessageResponse?> SendTeamsRequestAsync(object body)
        {
            var triggerUrl = _icmSettings.PostIncidentDiscussionUrl;
            if (string.IsNullOrEmpty(triggerUrl))
            {
                throw new Exception("ICM:PostIncidentDiscussionUrl is not configured.");
            }
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, triggerUrl);
            if (body != null)
            {
                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();
            var respContent = await response.Content.ReadAsStringAsync();
            var respBody = JsonConvert.DeserializeObject<TeamsPostMessageResponse>(respContent);
            return respBody;
        }
    }
}
