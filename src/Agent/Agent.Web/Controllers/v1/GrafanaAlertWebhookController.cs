using Microsoft.AspNetCore.Mvc;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Microsoft.Extensions.AI;
using System.Text.Json;

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
        /// Handles Grafana alert webhook notifications via POST
        /// </summary>
        /// <param name="request">Grafana alert webhook payload</param>
        /// <returns>Action result</returns>
        [HttpPost]
        public async Task<IActionResult> GrafanaAlert([FromBody] GrafanaAlertWebhookRequest request)
        {
            // Process the webhook notification
            try
            {
                var (threadId, existing) = await MergeSimilarAlerts(request);
                var alerts = JsonSerializer.Serialize(request.Alerts);
                if (existing)
                {
                    // Process the message in the background for existing thread
                    string message = $"I have received a similar alert with title {request.Title}, message {request.Message}, alerts: {alerts}, please analyze them together.";
                    ProcessAlertInBackground(threadId, message);

                    // If we have an existing thread, just return the thread ID
                    return Ok(new { threadId, message = "Alert merged, processing started" });
                }

                // Create the thread and post to Teams - wait for this to complete
                var thread = await inboundCommunicationService.CreateAlertThreadWithTeams(request.Title, $"Alert general message: {request.Message}, alerts in json: {alerts}");

                // Process the message in the background for new thread
                string newAlertMessage = "I have received an alert, please analyze it and give me the suggestion for actions to quickly mitigate the alerts.";
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
        public Dictionary<string, double> Values { get; set; } = new Dictionary<string, double>();
    }
}