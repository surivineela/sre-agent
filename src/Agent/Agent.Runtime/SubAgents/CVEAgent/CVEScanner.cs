// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.SubAgents.CVEAgent
{
    public class CVEScanner
    {
        private readonly ILogger<CVEScanner> _logger;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly IGraphDatabaseClient _graphDatabaseClient;
        private readonly IChatClientProvider _chatClientProvider;
        private readonly IGraphDBPlugin _graphDbPlugin;
        private readonly IGithubIssuePlugin _githubIssuePlugin;
        private readonly SinkService _sinkService;
        private readonly IThreadRepository _threadRepository;
        private readonly Kernel _kernel;

        public CVEScanner(
            ILogger<CVEScanner> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            IChatClientProvider chatClientProvider,
            IGraphDBPlugin graphDBPlugin,
            IGithubIssuePlugin githubIssuePlugin,
            SinkService sinkService,
            IThreadRepository threadRepository,
            Kernel kernel)
        {
            _logger = logger;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
            _chatClientProvider = chatClientProvider;
            _graphDbPlugin = graphDBPlugin;
            _githubIssuePlugin = githubIssuePlugin;
            _sinkService = sinkService;
            _threadRepository = threadRepository;
            _kernel = kernel;
        }

        public async Task Scan(CancellationToken cancellationToken)
        {
            var cveAgentContexts = (await _threadRepository.GetAllAgentContextsAsync())
                ?.Where(x => x.AgentType == AgentTypeEnum.CVE && x.ContextState != ContextStateEnum.Completed && x.ContextState != ContextStateEnum.Failed)
                ?.ToList();

            if (cveAgentContexts != null && cveAgentContexts.Count > 0)
            {
                _logger.LogInternalInformation("CVEAgent thread context already exists. Skipping scan.");
                return;
            }

            var unscannedQueryResults = await _graphDatabaseClient.Query(@"
                g.V().has('resourceType', 'microsoft.source/repository').has('isDeleted', false)
                .not(has('lastScanTime'))
                .values('resourceId')");

            var expiredScanQueryResults = await _graphDatabaseClient.Query($@"
                g.V().has('resourceType', 'microsoft.source/repository').has('isDeleted', false)
                .has('lastScanTime', lt('{DateTime.UtcNow.AddDays(-1)}'))
                .values('resourceId')");

            var repos = unscannedQueryResults
                .Select(x => (string)x)
                .OrderBy(resourceId => resourceId.Split("/").Last())
                .Union(expiredScanQueryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()))
                .ToList();

            if (repos.Count > 0)
            {
                (var thread, var agentContext) = await _agentInboundCommunicationService.CreateAgentThread(
                    "CVE Scanner",
                    """
                    Hi there! I found at least one repo that needs to be scanned for security vulnerabilties.

                    """,
                    agentTypeEnum: AgentTypeEnum.CVE);

                var cveAgent = new CVEAgent(
                    _chatClientProvider,
                    _graphDbPlugin,
                    _githubIssuePlugin,
                    _sinkService,
                    _threadRepository,
                    _kernel,
                    reposToScan: repos.Select(r => new RepoUrlStatus(r)).ToList());
                await cveAgent.PrepareAgentForUserInput(agentContext: agentContext);
            }
        }
    }
}

