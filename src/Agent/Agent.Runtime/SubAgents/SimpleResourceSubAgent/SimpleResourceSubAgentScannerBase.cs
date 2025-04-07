using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Grpc.Core;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents
{
    /// <summary>
    /// Base class for subagent proactive scanners. These generally inform the user when they discover something,
    /// then possibly kick off a workflow to plan a remediation.
    /// If you implement this class, then it will automatically be run on a timer.
    /// </summary>
    public abstract class SimpleResourceSubAgentScannerBase<TAgentType, TAgentInput, TActivity, TActivityInput>
        where TAgentType : SimpleResourceSubAgentBase<TAgentInput, TActivity, TActivityInput>
        where TAgentInput : SimpleResourceSubAgentInput<TActivityInput>, new()
        where TActivity : SimpleResourceSubAgentActivityBase<TActivityInput>
        where TActivityInput : SimpleResourceSubAgentActivityInput, new()
    {
        protected readonly ILogger<TAgentType> _logger;
        protected readonly DurableTaskClient _durableTaskClient;
        protected readonly SimpleResourceSubAgentFactoryBase<TAgentType, TAgentInput, TActivity, TActivityInput> _agentFactory;
        protected readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        protected readonly IGraphDatabaseClient _graphDatabaseClient;
        protected readonly ArmHelper _armHelper;

        public SimpleResourceSubAgentScannerBase(
            DurableTaskClient durableTaskClient,
            SimpleResourceSubAgentFactoryBase<TAgentType, TAgentInput, TActivity, TActivityInput> agentFactory,
            ILogger<TAgentType> logger,
            IAgentInboundCommunicationService agentInboundCommunicationService,
            IGraphDatabaseClient graphDatabaseClient,
            ArmHelper armHelper)
        {
            _logger = logger;
            _durableTaskClient = durableTaskClient;
            _agentFactory = agentFactory;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _graphDatabaseClient = graphDatabaseClient;
            _armHelper = armHelper;
        }

        /// <summary>
        /// How often should this scanner run?
        /// </summary>
        protected abstract TimeSpan RunInterval { get; }

        /// <summary>
        /// Method that pulls the resources that meet the criteria for this agent.
        /// Usually this would be a graph query, likely combined with an ARM call to check details.
        /// But it could be a call to an API or something else.
        /// </summary>
        /// <returns></returns>
        // TODO: It might make sense for each item returned to have specific violations, rather than
        // assuming the same for each item. Consider a set of storage accounts that have different
        // violations, for example.
        protected abstract Task<ICollection<SimpleResourceSubAgentResourceInformation>> GetResourcesInViolationAsync();

        /// <summary>
        /// When resource are found that we want to alert on, this is the boilerplate message that will be sent to the user.
        /// </summary>
        protected abstract string MessageWhenFoundResourcesInViolation { get; }

        /// <summary>
        /// Given a list of resources that are in violation, generate the input for the activity that will remediate
        /// them.
        /// </summary>
        /// <param name="Resources">A list of resources that have found to be in violation.</param>
        // TODO: It might make sense for this thing to be combined with GetResourcesInViolationAsync, so that
        // it can read not only the resources, but the particular details of the each violation.
        protected abstract TActivityInput GenerateActivityInput(IEnumerable<SimpleResourceSubAgentResourceInformation> Resources);

        public async Task ScanAsync(CancellationToken cancellationToken)
        {
            // Before running a new scan, ensure we're not currently mid-scan
            var agentName = typeof(TAgentType).Name;
            var runningAgents = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = _agentFactory.OrchestrationInstanceIdPrefix
            }).ToListAsync();

            if (runningAgents.Count > 0)
            {
                _logger.LogInformation($"{agentName} agent already running, skipping the scan.");
                return;
            }

            // Do the scan itself, likely querying graph and/or ARM.
            var appsInViolation = await GetResourcesInViolationAsync();

            // If found any, then send a message to the messaging thread.
            if (appsInViolation.Count > 0)
            {
                (var thread, var threadContext) = await _agentInboundCommunicationService.CreateAgentThread(
                    $"{agentName} found issues",
                    this.MessageWhenFoundResourcesInViolation,
                    AgentTypeEnum.DurableAgent
                );

                // TODO: At first I thought: We don't want to kick off a remediation activity here; that's too aggressive.
                // But after speaking with Paul, I'm realizing this doesn't have to be a remediation. It's a workflow that
                // makes a plan, gets authorization, and THEN remediates. So we can kick it off and maybe it stays around
                // for ages. But this isn't 100% clear yet.
                // If we do this, we need to make sure that our prompt creates a workflow that does in fact split things
                // up into these stages, like the TLS workflow.

                var input = GenerateActivityInput(
                    appsInViolation.Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                );

                var instanceId = await _agentFactory.StartOrchestration(input, threadContext);

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
