// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Agent.Runtime.SubAgents.DailyReportSummary
{
    public class DailyReportScanner
    {
        private readonly ILogger<DailyReportScanner> _logger;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadRepository _threadRepository;
        private readonly DailyReportSummaryAgentFactory _dailyReportSummaryAgentFactory;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly IGraphDatabaseClient _graphDatabaseClient;
        private static bool didItOnce = false;

        public DailyReportScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            DailyReportSummaryAgentFactory dailyReportSummaryAgentFactory,
            ILogger<DailyReportScanner> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient)
        {
            _logger = logger;
            _durableTaskClient = durableTaskClient;
            _threadRepository = threadRepository;
            _dailyReportSummaryAgentFactory = dailyReportSummaryAgentFactory;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
        }

        public async Task ScanAndGenerateReport(CancellationToken cancellationToken)
        {
            // Check if a report agent is already running
            var runningAgents = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = DailyReportSummaryAgentFactory.OrchestrationInstanceIdPrefix
            }).ToListAsync();

            if (runningAgents.Count > 0)
            {
                _logger.LogInformation("Daily report summary agent already running, skipping this run.");
                return;
            }

            // Check if we need to run the daily report (e.g., only during certain hours)
            var now = DateTime.UtcNow;
            var todayReportTime = new DateTime(now.Year, now.Month, now.Day, 7, 0, 0, DateTimeKind.Utc); // 7 AM UTC

            // Skip if it's not time yet for the daily report
            if (now.Hour != todayReportTime.Hour && didItOnce)
            {
                _logger.LogDebug("Not time for daily report yet. Current hour: {CurrentHour}, Target hour: {TargetHour}",
                    now.Hour, todayReportTime.Hour);
                return;
            }

            didItOnce = true;

            // Get the list of resource types from the knowledge graph
            var queryResults = await _graphDatabaseClient.Query("g.V().groupCount().by('resourceType')");

            // Extract resource types and counts
            /*
            Dictionary<string, int> resourceCounts = new Dictionary<string, int>();
            if (queryResults.Count > 0 && queryResults[0] is Dictionary<string, object> countDict)
            {
                foreach (var kvp in countDict)
                {
                    if (kvp.Value is long count)
                    {
                        resourceCounts[kvp.Key] = (int)count;
                    }
                }
            }

            Dictionary<string, int> resourceCounts = new Dictionary<string, int>();

            // Create a thread for the report
            var dateFormatted = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var thread = await _agentInboundCommunicationService.CreateAgentThread(
                $"Daily Resources Report - {dateFormatted}",
                "Starting Automated generation of daily report showing resource metrics and status.");

            // Prepare the input for the agent
            var input = new DailyReportSummaryInput
            {
                ReportType = "Daily",
                ResourceTypesToInclude = resourceCounts.Keys.ToList(),
                MetricsToInclude = new List<string> { "cpu", "memory", "requests", "errors" },
                Timespan = "1d",
                GrafanaSettings = new GrafanaConfig
                {
                    GrafanaUrl = "http://localhost:3000", // Should be configured from settings
                    PrometheusUrl = "http://localhost:9090/metrics", // Should be configured from settings
                    DataSourceName = "KnowledgeGraph"
                }
            };

            // Start the agent orchestration
            var instanceId = await _dailyReportSummaryAgentFactory.StartOrchestration(input, thread.Id.ToString());

            _logger.LogInformation("Started daily report generation with instance ID: {InstanceId}", instanceId);

            // Wait for completion or handle timeout
            try
            {
                using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromHours(1))) // 1 hour timeout
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
                {
                    await _durableTaskClient.WaitForInstanceCompletionAsync(instanceId, linkedCts.Token);
                    _logger.LogInformation("Daily report generation completed successfully.");
                }
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Daily report generation was cancelled.");
                }
                else
                {
                    _logger.LogWarning("Daily report generation timed out.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for daily report generation: {Message}", ex.Message);
            }
        }
    }
}
*/
