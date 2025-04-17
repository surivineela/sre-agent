// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.AspNetCore.Mvc;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class GrafanaAlertWebhookController(
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        IChatClient chatClient,
        ILogger<GrafanaAlertWebhookController> logger
    ) : ControllerBase
    {

        /// <summary>
        /// Handles Grafana alert webhook notifications via POST or PUT
        /// </summary>
        /// <param name="request">Grafana alert webhook payload (can be null)</param>
        /// <returns>Action result</returns>
        [HttpPost]
        [HttpPut]
        public async Task<IActionResult> GrafanaAlert([FromBody] GrafanaAlertWebhookRequest? request)
        {
            // Create a default request if none provided
            request ??= new GrafanaAlertWebhookRequest
            {
                Title = "Unknown Alert",
                Message = "Empty alert payload received",
                Alerts = new List<Alert>()
            };
            if (request.Alerts == null || request.Alerts.Count == 0)
            {
                logger.LogWarning("Received Grafana alert webhook with no alerts");
                return BadRequest("No alerts found in the request");
            }

            // Process the webhook notification
            try
            {
                var (threadId, existing) = await MergeSimilarAlerts(request);
                var alertInfoBuilder = new StringBuilder();
                alertInfoBuilder.AppendLine($"# Title: {request.Title}\n\n");
                if (!string.IsNullOrWhiteSpace(request.Message))
                {
                    alertInfoBuilder.AppendLine($"* Message: {request.Message}\n\n");
                }
                alertInfoBuilder.AppendLine($"* Status: {request.Status} - State: {request.State}\n\n");

                for (int i = 0; i < request.Alerts.Count; i++)
                {
                    var alert = request.Alerts[i];
                    string alertName;
                    alert.Labels.TryGetValue("alertname", out alertName);
                    if (string.IsNullOrEmpty(alertName))
                    {
                        alertName = $"Alert {i + 1}";
                    }

                    alertInfoBuilder.AppendLine($"### {alertName}: `{alert.Status}`\n");


                    // Add key annotations
                    if (alert.Annotations.Count > 0)
                    {
                        var priorityAnnotations = new[] { "summary", "description" };
                        foreach (var key in priorityAnnotations)
                        {
                            if (alert.Annotations.TryGetValue(key, out var value))
                            {
                                if (key == "summary")
                                {
                                    value.Split('|').ToList().ForEach(x =>
                                    {
                                        var parts = x.Split(new[] { '=' }, 2);
                                        if (parts.Length == 2)
                                        {
                                            alertInfoBuilder.AppendLine($"- **{parts[0]}:** {parts[1]}\n");
                                        }
                                        else
                                        {
                                            alertInfoBuilder.AppendLine($"- **{key}:** {x}\n");
                                        }
                                    });
                                }
                            }
                        }
                    }

                    alertInfoBuilder.AppendLine($"**Started at:** {alert.StartsAt}");

                    // Add separator between alerts
                    if (i < request.Alerts.Count - 1)
                    {
                        alertInfoBuilder.AppendLine("\n---\n");
                    }
                }

                var alertInfo = alertInfoBuilder.ToString();
                if (existing)
                {
                    // Process the message in the background for existing thread
                    string message = $"Received a similar alert: {alertInfo}, please delegate to Kubernetes Agent to analyze them together.";
                    if (string.Equals(request.Status, "resolved", StringComparison.OrdinalIgnoreCase))
                    {
                        message = $"Got alert resolved message: {alertInfo}, please keep monitoring the system to make sure all components are healthy.";
                    }
                    ProcessAlertInBackground(threadId, message);

                    // If we have an existing thread, just return the thread ID
                    return Ok(new { threadId, message = "Alert merged, processing started" });
                }

                // Create the thread and post to Teams - wait for this to complete
                var thread = await inboundCommunicationService.CreateAlertThreadWithTeams(request.Title, alertInfo, AgentTypeEnum.Meta);

                // Process the message in the background for new thread
                string newAlertMessage = "I have received an alert, please delegate to Kubernetes Agent to diagnose and give me the suggestion for actions to quickly mitigate the alerts.";
                ProcessAlertInBackground(thread.Id, newAlertMessage);

                // Return immediately after thread creation and Teams notification
                return Ok(new { threadId = thread.Id, message = "New alert received, processing started" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating alert thread for {Title}", request.Title);
                return StatusCode(500, "Failed to process alert");
            }
        }

        private void ProcessAlertInBackground(Guid threadId, string messageContent)
        {
            // Process the message in the background - don't await this
            _ = Task.Run(async () =>
            {
                try
                {
                    await inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                        threadId,
                        Guid.NewGuid(),
                        messageContent,
                        "alert",
                        "Alert Prompt",
                        DateTime.UtcNow
                    ));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing alert thread {ThreadId} in background", threadId);
                }
            });
        }

        /// <summary>
        /// Merges similar alerts into one thread, usually alerts will happen frequently in a short time
        /// Also one outage can cause multiple different alerts which has similarity
        /// </summary>
        private async Task<(Guid, bool)> MergeSimilarAlerts(GrafanaAlertWebhookRequest req)
        {
            // TODO(jianbosun): 
            // A better implementation could be:
            // 1. Use repository to list all active threads in the past 12 hours, apply OData filters
            // 2. Get the title and initial message for each thread
            // 3. Use LLM to decide if the new alert is similar to any existing thread by alert title and message
            // we can also use RAG for it to make the response quickly
            var threads = await repository.GetThreadsAsync();
            foreach (var thread in threads)
            {
                if (thread.Title.Contains(req.Title))
                {
                    // Found a similar alert thread
                    return (thread.Id, true);
                }
            }
            return (Guid.NewGuid(), false);
        }
    }

    public class GrafanaAlertWebhookRequest
    {
        public string Receiver { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int OrgId { get; set; }
        public List<Alert> Alerts { get; set; } = new List<Alert>();
        public Dictionary<string, string> GroupLabels { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> CommonLabels { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> CommonAnnotations { get; set; } = new Dictionary<string, string>();
        public string ExternalURL { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string GroupKey { get; set; } = string.Empty;
        public int TruncatedAlerts { get; set; }
        public string Title { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class Alert
    {
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();
        public string StartsAt { get; set; } = string.Empty;
        public string EndsAt { get; set; } = string.Empty;
        public string GeneratorURL { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public string SilenceURL { get; set; } = string.Empty;
        public string DashboardURL { get; set; } = string.Empty;
        public string PanelURL { get; set; } = string.Empty;
        public Dictionary<string, double>? Values { get; set; } = null;
        public String? ValueString { get; set; } = string.Empty;
    }
}
