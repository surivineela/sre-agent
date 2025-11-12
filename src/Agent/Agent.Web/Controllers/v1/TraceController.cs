// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/[controller]")]
    public class TraceController : ControllerBase
    {
        private readonly ICollection<Activity> _exportedActivities;
        private readonly ILogger<TraceController> _logger;

        public TraceController(
            ICollection<Activity> exportedActivities,
            ILogger<TraceController> logger)
        {
            _exportedActivities = exportedActivities;
            _logger = logger;
        }

        [HttpPost("fetch")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: test api
        public ActionResult<List<TraceResponse>> GetTraces([FromBody] TraceRequest request)
#pragma warning restore CUSTOM004

        {
            _logger.LogInternalInformation("Fetching exported traces for threadId: {ThreadId}", request.ThreadId);

            // Filter activities by threadId attribute
            var filteredActivities = _exportedActivities
                .Where(activity => activity.Tags.Any(tag =>
                    tag.Key.Equals("thread.id", StringComparison.OrdinalIgnoreCase) &&
                    tag.Value != null &&
                    tag.Value.Equals(request.ThreadId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (!filteredActivities.Any())
            {
                _logger.LogInternalInformation("No traces found for threadId: {ThreadId}", request.ThreadId);
                return Ok(new List<TraceResponse>());
            }

            // Group by TraceId and process all traces
            var traceGroups = filteredActivities.GroupBy(a => a.TraceId);
            var traceResponses = new List<TraceResponse>();

            foreach (var traceGroup in traceGroups)
            {
                var traceActivities = traceGroup.ToList();
                var totalDuration = (int)traceActivities.Sum(a => a.Duration.TotalMilliseconds);

                // Convert activities to spans
                var spans = traceActivities.Select(activity => new Span
                {
                    SpanId = activity.SpanId.ToString(),
                    ParentSpanId = activity.ParentSpanId.ToString(),
                    OperationName = activity.OperationName ?? activity.DisplayName ?? "Unknown",
                    StartTime = new DateTimeOffset(activity.StartTimeUtc).ToUnixTimeMilliseconds(),
                    Duration = (int)activity.Duration.TotalMilliseconds,
                    Status = activity.Status.ToString(),
                    Attributes = activity.Tags.ToDictionary(kvp => kvp.Key, kvp => (object)(kvp.Value ?? "")),
                    Events = activity.Events.Select(e => new SpanEvent
                    {
                        Name = e.Name,
                        Timestamp = e.Timestamp.ToUnixTimeMilliseconds(),
                        Attributes = e.Tags.ToDictionary(kvp => kvp.Key, kvp => (object)(kvp.Value ?? ""))
                    }).ToList()
                }).ToList();

                var traceResponse = new TraceResponse
                {
                    TraceId = traceGroup.Key.ToString(),
                    TotalDuration = totalDuration,
                    SpanCount = spans.Count,
                    Metadata = new TraceMetadata
                    {
                        Environment = "Development", // You can extract this from activity tags if available
                        Version = "1.0.0", // You can extract this from activity tags if available
                        Region = "Unknown" // You can extract this from activity tags if available
                    },
                    Spans = spans
                };

                traceResponses.Add(traceResponse);
            }

            _logger.LogInternalInformation("Returning {TraceCount} traces with total {SpanCount} spans for threadId: {ThreadId}",
                traceResponses.Count,
                traceResponses.Sum(t => t.SpanCount),
                request.ThreadId);

            return Ok(traceResponses);
        }

        [HttpGet("health")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: test api
        public ActionResult CheckHealth()
#pragma warning restore CUSTOM004
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        [HttpDelete("clear")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: test api
        public ActionResult ClearTraces()
#pragma warning restore CUSTOM004
        {
            _logger.LogInternalInformation("Clearing exported traces");
            _exportedActivities.Clear();
            return Ok(new { message = "Traces cleared successfully" });
        }
    }

    public class TraceRequest
    {
        [JsonPropertyName("agentId")]
        public string AgentId { get; set; } = string.Empty;

        [JsonPropertyName("threadId")]
        public string ThreadId { get; set; } = string.Empty;
    }

    public class TraceResponse
    {
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = string.Empty;

        [JsonPropertyName("totalDuration")]
        public int TotalDuration { get; set; }

        [JsonPropertyName("spanCount")]
        public int SpanCount { get; set; }

        [JsonPropertyName("metadata")]
        public TraceMetadata Metadata { get; set; } = new();

        [JsonPropertyName("spans")]
        public List<Span> Spans { get; set; } = new();
    }

    public class TraceMetadata
    {
        [JsonPropertyName("environment")]
        public string Environment { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;
    }

    public class Span
    {
        [JsonPropertyName("spanId")]
        public string SpanId { get; set; } = string.Empty;

        [JsonPropertyName("parentSpanId")]
        public string? ParentSpanId { get; set; }

        [JsonPropertyName("operationName")]
        public string OperationName { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public long StartTime { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("attributes")]
        public Dictionary<string, object> Attributes { get; set; } = new();

        [JsonPropertyName("events")]
        public List<SpanEvent> Events { get; set; } = new();
    }

    public class SpanEvent
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("attributes")]
        public Dictionary<string, object> Attributes { get; set; } = new();
    }
}
