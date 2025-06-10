using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
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
            // Do the scan itself, likely querying graph and/or ARM.
            var appsInViolation = await GetResourcesInViolationAsync();
            var groupedAppsInViolation = appsInViolation.GroupBy(x => x.ResourceProviderName).ToList();
            foreach(var group in groupedAppsInViolation)
            {
                var resourceProviderName = group.Key; // eg; "microsoft.storage/storageaccounts"
                if (group.Count() > 0)
                {
                    var orchestrationSuffix = resourceProviderName.Replace("/", "-");
                    var instancePrefixPerProvider = _agentFactory.OrchestrationInstanceIdPrefix + orchestrationSuffix;

                    // Before running a new scan, ensure we're not currently mid-scan
                    var agentName = typeof(TAgentType).Name;
                    var runningAgents = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
                    {
                        Statuses = new[] { OrchestrationRuntimeStatus.Running },
                        InstanceIdPrefix = instancePrefixPerProvider,
                    }).ToListAsync();

                    if (runningAgents.Count > 0)
                    {
                        _logger.LogInternalInformation($"{instancePrefixPerProvider} agent already running, skipping the scan.");
                        continue;
                    }

                    // TODO: We probably want to reuse threads per-resource provider.
                    (var thread, var agentContext) = await _agentInboundCommunicationService.CreateAgentThread(
                        $"{agentName} for {resourceProviderName} found issues",
                        this.MessageWhenFoundResourcesInViolation,
                        AgentTypeEnum.DTS
                    );

                    var input = GenerateActivityInput(
                        group.Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                    );

                    // When starting an orchestration, we're doing so just for this provider (eg; storage, eventhub, etc.).
                    // Therefore we pass the provider name as the instanceIdSuffix, so that on the next scanner run, it will
                    // avoid starting a new orchestration if one is already running for that provider.
                    var instanceId = await _agentFactory.StartOrchestration(input, agentContext.ThreadId, instanceIdSuffix: orchestrationSuffix);

                    /*
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
                            _logger.LogInternalError(ex, "Error while waiting for instance completion: {Message}", ex.Message);
                            await Task.Delay(1000, cancellationToken);
                        }
                    }*/
                }
            }
            // If found any, then send a message to the messaging thread.
        }
    }
}
