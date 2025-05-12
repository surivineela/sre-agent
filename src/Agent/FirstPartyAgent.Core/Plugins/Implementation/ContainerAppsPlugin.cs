// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Agent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Plugins
{
    public class ContainerAppsPlugin : IContainerAppsPlugin
    {
        private readonly ILogger<ContainerAppsPlugin> _logger;
        private readonly ICMWorkflowClient _icmWorkflowClient;
        private readonly HttpClient _httpClient = new HttpClient();

        public ContainerAppsPlugin(ILogger<ContainerAppsPlugin> logger, ICMWorkflowClient icmWorkflowClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _icmWorkflowClient = icmWorkflowClient ?? throw new ArgumentNullException(nameof(icmWorkflowClient));
        }

        public async Task<SubscriptionDetail?> GetSubscriptionDetail(string subscriptionId)
        {
            _logger.LogInformation($"GetSubscriptionDetail Started: {subscriptionId}");
            return await _icmWorkflowClient.GetSubscriptionDetail(subscriptionId);
        }

        public async Task<AcaSubscriptionUsage?> GetSubscriptionUsage(string subsriptionId)
        {
            return await _icmWorkflowClient.GetSubscriptionUsage(subsriptionId);
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
            var triggerUrl = Environment.GetEnvironmentVariable("AppSettings__Core__External__ICMWorkflows__PostIncidentDiscussionUrl") ?? string.Empty;
            if (string.IsNullOrEmpty(triggerUrl))
            {
                throw new Exception("ICM:PostIncidentDiscussionUrl is not configured.");
            }
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, triggerUrl);
            if (body != null)
            {
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();
            var respContent = await response.Content.ReadAsStringAsync();
            var respBody = JsonSerializer.Deserialize<TeamsPostMessageResponse>(respContent);
            return respBody;
        }
    }
}
