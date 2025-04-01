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

namespace Agent.Runtime.SubAgents.CVEAgent
{
    public class CVEScanner
    {
        private readonly ILogger<CVEScanner> _logger;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadRepository _threadRepository;
        private readonly CVEAgentFactory _cveAgentFactory;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly IGraphDatabaseClient _graphDatabaseClient;

        public CVEScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            CVEAgentFactory cveAgentFactor,
            ILogger<CVEScanner> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient)
        {
            _logger = logger;
            _durableTaskClient = durableTaskClient;
            _threadRepository = threadRepository;
            _cveAgentFactory = cveAgentFactor;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
        }

        public async Task Scan(CancellationToken cancellationToken)
        {
            var runningAgents = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = CVEAgentFactory.OrchestrationInstanceIdPrefix
            }).ToListAsync();

            if (runningAgents.Count > 0)
            {
                _logger.LogInformation("CVE agent already running, skipping the scan.");
                return;
            }

            var queryResults = await _graphDatabaseClient.Query(@"
                g.V().has('resourceType', 'microsoft.source/repository')
                .values('resourceId')");

            var repos = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();

            if (repos.Count > 0)
            {
                var thread = await _agentInboundCommunicationService.CreateAgentThread(
                    "CVE Scanner",
                    """
                    Hi there! I found at least one repo that needs to be scanned for security vulnerabilties.

                    """);


                var input = new CVEInput()
                {
                    ReposToScan = repos.Select(r => new RepoUrlStatus(r)).ToList(),
                };

                var threadContext = new ThreadContext(thread.Id);

                var instanceId = await _cveAgentFactory.StartOrchestration(input, threadContext);

                // work around "bad grpc response 504" error
                bool completed = false;
                while (!completed)
                {
                    try
                    {
                        await _durableTaskClient.WaitForInstanceCompletionAsync(instanceId, cancellationToken);
                        completed = true;
                    }
                    catch (RpcException ex)
                    {
                        _logger.LogError(ex, "Error while waiting for instance completion: {Message}", ex.Message);
                        await Task.Delay(1000, cancellationToken);
                    }
                }

            }
        }
    }
}
