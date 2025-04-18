// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.SourceCodeAgent
{
    public class SourceCodeScanner
    {
        private readonly ILogger<SourceCodeScanner> _logger;
        private readonly IThreadRepository _threadRepository;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly IGraphDatabaseClient _graphDatabaseClient;
        private readonly SinkService _sinkService;
        private readonly IGraphDBPlugin _graphDbPlugin;
        private readonly IChatClient _chatClient;

        public SourceCodeScanner(
            IThreadRepository threadRepository,
            ILogger<SourceCodeScanner> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            SinkService sinkService,
            IGraphDBPlugin graphDbPlugin,
            IChatClient chatClient)
        {
            _logger = logger;
            _threadRepository = threadRepository;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
            _sinkService = sinkService;
            _graphDbPlugin = graphDbPlugin;
            _chatClient = chatClient;
        }

        public async Task Scan(CancellationToken cancellationToken)
        {
            var sourceCodeAgentV2ThreadContext = (await _threadRepository.GetThreadContextsAsync())
                ?.Where(x => x.AgentTypeEnum == AgentTypeEnum.SourceCode && x.IsThreadActive)
                ?.ToList();

            if (sourceCodeAgentV2ThreadContext != null && sourceCodeAgentV2ThreadContext.Count > 0)
            {
                _logger.LogInformation("SourceCodeAgentV2 thread context already exists. Skipping scan.");
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
                (var thread, var agentContext, var threadContext) = await _agentInboundCommunicationService.CreateAgentThread(
                    "SourceCode",
                    """
                    Hi there! I found at least one Container App that does not have the source code repo url provided.
                    Preparing details...  
                    """,
                    agentTypeEnum: AgentTypeEnum.SourceCode);

                var sourceCodeAgent = new SourceCodeAgent(
                    _chatClient,
                    _graphDbPlugin,
                    sinkService: _sinkService,
                    repository: _threadRepository,
                    appsWithoutSourceCodeNodes: resources.Select(r => new SourceCodeStatus(r)).ToList());

                await sourceCodeAgent.PrepareAgentForUserInput(agentContextId: agentContext.Id, threadContext);

                sourceCodeAgent.InitChatHistoryFromMessageQueue(threadContext.RecentMessages);
            }
        }
    }
}

