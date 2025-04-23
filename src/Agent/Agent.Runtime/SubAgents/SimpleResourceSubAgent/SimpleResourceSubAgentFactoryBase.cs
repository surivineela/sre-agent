using Agent.Runtime.Communication;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using System.Linq.Expressions;

namespace Agent.Runtime.SubAgents
{
    /// <summary>
    /// Implement this class to have your agent registered with the system and kicked off as required.
    /// </summary>
    /// <remarks>
    /// While the constructor requires a few type arguments, you will likely add several of your own to
    /// provide the tools you need to do your work.
    /// </remarks>
    /// <typeparam name="TAgentType">The class of your main agent code that derives from <see cref="SimpleResourceSubAgentBase{TInput, TActivity, TActivityInput}"/></typeparam>
    public abstract class SimpleResourceSubAgentFactoryBase<TAgentType, TAgentInput, TActivity, TActivityInput>
        where TAgentType : SimpleResourceSubAgentBase<TAgentInput, TActivity, TActivityInput>
        where TAgentInput : SimpleResourceSubAgentInput<TActivityInput>, new()
        where TActivity : SimpleResourceSubAgentActivityBase<TActivityInput>
        where TActivityInput : SimpleResourceSubAgentActivityInput, new()
    {
        private readonly IThreadOrchestrationManager _mappingManager;
        private readonly DurableTaskClient _durableTaskClient;
        ToolsRepository _toolsRepository;

        protected SimpleResourceSubAgentFactoryBase(ToolsRepository toolsRepository, IThreadOrchestrationManager mappingManager, DurableTaskClient durableTaskClient)
        {
            _mappingManager = mappingManager;
            _durableTaskClient = durableTaskClient;
            _toolsRepository = toolsRepository;
        }

        /// <summary>
        /// Provide the list of tools that your activity will need. Generally, each tool will come from a plugin that
        /// you accepted in the constructor.
        /// </summary>
        /// <remarks>
        /// Example:
        ///     var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
        ///     yield return () => remediationPluginDefinition.StorageAccountDisableSharedKeySupport;
        ///     yield return () => remediationPluginDefinition.StorageAccountDisablePublicContainers;
        /// </remarks>
        protected abstract IEnumerable<Expression<Func<Delegate>>> GetToolList();

        public string OrchestrationInstanceIdPrefix => typeof(TAgentType).Name;

        public async Task<string> StartOrchestration(TActivityInput input, Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";
            var threadIdStr = threadId.ToString();

            await _mappingManager.AddMappingAsync(threadIdStr, instanceId);

            // Generate the tool signatures from the supplied lambdas
            var toolSignatures = GetToolList()
                .Select(x => ToolsRepository.GetSignature(x))
                .ToList();

            var agentInput = new TAgentInput
            {
                ActivityInput = input,
                ToolSignatures = toolSignatures,
                ThreadId = threadId
            };

            return await _durableTaskClient.ScheduleNewOrchestrationInstanceAsync(
                OrchestrationInstanceIdPrefix,
                agentInput,
                new StartOrchestrationOptions(InstanceId: instanceId)
            );
        }
    }
}
