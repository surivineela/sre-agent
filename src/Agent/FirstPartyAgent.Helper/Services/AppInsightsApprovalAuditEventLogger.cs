// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Helper.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace FirstPartyAgent.Helper.Services
{
    /// <summary>
    /// Note: Not the ideal way to log audit events as App Insight does not guarantee delivery of events
    /// </summary>
    public class AppInsightsApprovalAuditEventLogger : IApprovalAuditEventLogger
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<AppInsightsApprovalAuditEventLogger> _logger;

        public AppInsightsApprovalAuditEventLogger(ILogger<AppInsightsApprovalAuditEventLogger> logger, IConfiguration configuration)
        {
            string? connectionString = configuration["AppInsights:ConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ArgumentException("Application Insights connection string must be set in configuration or environment variable 'APPLICATIONINSIGHTS_CONNECTION_STRING'.");
                }
            }

            _telemetryClient = new TelemetryClient(new TelemetryConfiguration
            {
                ConnectionString = connectionString
            });
            _logger = logger;
        }

        public async Task LogEventAsync(ApprovalAuditEvent auditEvent)
        { 
            var eventTelemetry = new EventTelemetry();
            if (auditEvent is ApprovalCreationRequestAuditEvent creationEvent)
            {
                eventTelemetry.Name = "ApprovalCreationRequest";
            }
            else if(auditEvent is ApprovalActionAuditEvent actionEvent)
            {
                eventTelemetry.Name = "ApprovalAction";
            }
            else
            {
                _logger.LogError("Unsupported audit event type: {AuditEventType}", auditEvent.GetType().Name);
                throw new InvalidOperationException($"Unsupported audit event type: {auditEvent.GetType().Name}");
            }

            var token = JToken.FromObject(auditEvent);

            foreach (var property in token.Children<JProperty>())
            {
                eventTelemetry.Properties[property.Name] = property.Value.ToString();
            }

            _telemetryClient.TrackEvent(eventTelemetry);
            await _telemetryClient.FlushAsync(CancellationToken.None);
            _logger.LogInformation("Successfully logged approval audit event {EventId} to Application Insights.", auditEvent.ApprovalDocumentId);
        }
    }
}
