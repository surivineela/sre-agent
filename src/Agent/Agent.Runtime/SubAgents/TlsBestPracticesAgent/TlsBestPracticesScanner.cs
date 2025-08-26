// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.TlsBestPracticesAgent
{
    public class TlsBestPracticesScanner
    {
        private readonly ILogger<TlsBestPracticesScanner> _logger;
        private readonly IThreadRepository _threadRepository;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly IGraphDatabaseClient _graphDatabaseClient;
        private readonly ArmHelper _armHelper;
        private readonly CoreSettings _coreSettings;

        public TlsBestPracticesScanner(
            IThreadRepository threadRepository,
            ILogger<TlsBestPracticesScanner> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            ArmHelper armHelper,
            CoreSettings coreSettings)
        {
            _logger = logger;
            _threadRepository = threadRepository;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
            _armHelper = armHelper;
            _coreSettings = coreSettings;
        }

        public async Task Scan(CancellationToken cancellationToken)
        {
            var queryResults = await _graphDatabaseClient.Query("g.V().has('resourceType', 'microsoft.web/sites').has('isDeleted', false).values('resourceId')");

            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();
            _logger.LogInternalInformation("Found Web Apps / Function Apps in the graph database. {Apps}", string.Join(", ", resources));

            // TODO - remove.
            // some temp filtering because Paul has too many resources
            resources.RemoveAll(x => x.StartsWith("/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/", StringComparison.InvariantCultureIgnoreCase) && !x.Contains("-demo", StringComparison.InvariantCultureIgnoreCase));

            var tlsSettings = await _armHelper.GetTlsSettings(resources);
            var appsInViolation = tlsSettings.Where(x => x.MinimumTlsVersion != null && new Version(x.MinimumTlsVersion) < new Version("1.2"))
                .ToList();

            _logger.LogInternalInformation("Found {Count} apps in violation of TLS best practices.", appsInViolation.Count);

            if (appsInViolation.Count > 0)
            {
                (var thread, var agentContext) = await _agentInboundCommunicationService.CreateAgentThread(
                    "TLS Best Practices",
                    """
                    Hi there! I found Web Apps / Function Apps that are allowing TLS connections below the recommended minimum version.
                    For more information on Microsoft's cryptographic recommendations see:
                    https://learn.microsoft.com/en-us/security/engineering/cryptographic-recommendations#tlsssl-versions

                    Preparing details...
                    """,
                    agentTypeEnum: AgentTypeEnum.DTS);


                var input = new TlsBestPracticesInput()
                {
                    AppsInViolation = appsInViolation,
                    DesiredVersion = "1.2"
                };

                _logger.LogInternalInformation("Using Agent Framework to process tls best practices agent.");

                var existingAppsDetails = string.Join(Environment.NewLine,
                    input.AppsInViolation.Select(x => $"{x.ResourceId} has a current minimum TLS version of {x.MinimumTlsVersion}. Proceed and update it if necessary."));

                var message = new ThreadMessage(
                    ThreadId: agentContext.ThreadId,
                    AgentContextId: agentContext.Id,
                    MessageId: Guid.NewGuid(),
                    Message: existingAppsDetails,
                    UserId: "",
                    DisplayName: "",
                    Timestamp: DateTime.UtcNow);
                await _agentInboundCommunicationService.ProcessUserMessageAsync(message);

            }
        }
    }
}

