using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Grpc.Core;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.SubAgents.TlsBestPracticesAgent
{
    public class TlsBestPracticesScanner
    {
        private readonly ILogger<TlsBestPracticesScanner> _logger;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadRepository _threadRepository;
        private readonly TlsBestPracticeAgentFactory _tlsBestPracticeAgentFactory;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly IGraphDatabaseClient _graphDatabaseClient;
        private readonly ArmHelper _armHelper;

        public TlsBestPracticesScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            TlsBestPracticeAgentFactory tlsBestPracticeAgentFactory,
            ILogger<TlsBestPracticesScanner> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            ArmHelper armHelper)
        {
            _logger = logger;
            _durableTaskClient = durableTaskClient;
            _threadRepository = threadRepository;
            _tlsBestPracticeAgentFactory = tlsBestPracticeAgentFactory;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
            _armHelper = armHelper;
        }


        public async Task Scan(CancellationToken cancellationToken)
        {

            var runningAgents = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = TlsBestPracticeAgentFactory.OrchestrationInstanceIdPrefix
            }).ToListAsync();

            if(runningAgents.Count > 0)
            {
                _logger.LogInformation("TlsBestPractices agent already running, skipping the scan.");
                return;
            }

            var queryResults = await _graphDatabaseClient.Query("g.V().has('resourceType', 'microsoft.web/sites').values('resourceId')");

            var resources = queryResults.Select(x => (string) x).ToList();

            // TODO - remove.
            // some temp filtering because Paul has too many resources
            resources.RemoveAll(x => x.StartsWith("/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/", StringComparison.InvariantCultureIgnoreCase) && !x.Contains("-demo", StringComparison.InvariantCultureIgnoreCase));

            var tlsSettings = await _armHelper.GetTlsSettings(resources);
            var appsInViolation = tlsSettings.Where(x => new Version(x.MinimumTlsVersion) < new Version("1.2"))
                .ToList();

            if (appsInViolation.Count > 0)
            {
                var thread = await _agentInboundCommunicationService.CreateAgentThread(
                    "TLS Best Practices", 
                    "Some applications that do not follow best practices for TLS configuration have been found.");

                var input = new TlsBestPracticesInput()
                {
                    AppsInViolation = appsInViolation,
                    DesiredVersion = "1.2"
                };

                var instanceId = await _tlsBestPracticeAgentFactory.StartOrchestration(input, thread.Id.ToString());

                // work around "bad grpc response 504" error
                bool completed = false;
                while (!completed)
                {
                    try
                    {
                        await _durableTaskClient.WaitForInstanceCompletionAsync(instanceId, cancellationToken);
                        completed = true;
                    }
                    catch(RpcException ex)
                    {
                        _logger.LogError(ex, "Error while waiting for instance completion: {Message}", ex.Message);
                        await Task.Delay(1000, cancellationToken);
                    }
                }

            }
        }
    }
}
