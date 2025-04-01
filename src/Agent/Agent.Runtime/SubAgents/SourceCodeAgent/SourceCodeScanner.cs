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

namespace Agent.Runtime.SubAgents.SourceCodeAgent
{
    public class SourceCodeScanner
    {
        private readonly ILogger<SourceCodeScanner> _logger;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadRepository _threadRepository;
        private readonly SourceCodeAgentFactory _sourceCodeAgentFactory;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly IGraphDatabaseClient _graphDatabaseClient;

        public SourceCodeScanner(
            DurableTaskClient durableTaskClient,
            IThreadRepository threadRepository,
            SourceCodeAgentFactory sourceCodeAgentFactory,
            ILogger<SourceCodeScanner> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient)
        {
            _logger = logger;
            _durableTaskClient = durableTaskClient;
            _threadRepository = threadRepository;
            _sourceCodeAgentFactory = sourceCodeAgentFactory;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
        }

        public async Task Scan(CancellationToken cancellationToken)
        {
            var runningAgents = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = SourceCodeAgentFactory.OrchestrationInstanceIdPrefix
            }).ToListAsync();

            if (runningAgents.Count > 0)
            {
                _logger.LogInformation("SourceCode agent already running, skipping the scan.");
                return;
            }

            var queryResults = await _graphDatabaseClient.Query(@"
                g.V().has('resourceType', 'microsoft.app/containerapps')
                .not(outE().hasLabel('SERVES_CODE').inV().has('resourceType', 'microsoft.source/repository'))
                .values('resourceId')");

            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();

            // TODO - remove.
            // some temp filtering because Paul has too many resources
            resources.RemoveAll(x => x.StartsWith("/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/", StringComparison.InvariantCultureIgnoreCase) && !x.Contains("-demo", StringComparison.InvariantCultureIgnoreCase));

            if (resources.Count > 0)
            {
                var thread = await _agentInboundCommunicationService.CreateAgentThread(
                    "SourceCode",
                    """
                    Hi there! I found at least one Container App that does not have the source code repo url provided.
                    Preparing details...  
                    """);


                var input = new SourceCodeInput()
                {
                    AppsWithoutSourceCodeNodes = resources.Select(r => new SourceCodeStatus(r)).ToList(),
                };

                var threadContext = new ThreadContext(thread.Id);

                var instanceId = await _sourceCodeAgentFactory.StartOrchestration(input, threadContext);

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
