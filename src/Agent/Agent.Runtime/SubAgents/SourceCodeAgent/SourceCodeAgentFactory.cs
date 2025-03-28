using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Agent.Core.Models;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents.SourceCodeAgent;

namespace Agent.Runtime.SubAgents.SourceCodeAgent;

// [Export]
public sealed class SourceCodeAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(SourceCodeAgent);

    public SourceCodeAgentFactory(
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient,
        IGraphDBPlugin graphDbPlugin)
    {
        var toolSignatures = new List<string>();

        var graphDbPluginDefinition = new GraphDBPluginDefinition(graphDbPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => graphDbPluginDefinition.AddSourceCodeNodeToContainerAppNode));
        toolSignatures.Add(toolsRepository.GetSignature(() => graphDbPluginDefinition.GetContainerAppsWithNodesWithoutSourceCodeNodes));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        SourceCodeInput input,
        string threadId = "")
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        if (threadId != null)
        {
            await _mappingManager.AddMappingAsync(new ThreadOrchestrationMapping(
                Id: $"mapping_{threadId}",
                ThreadId: threadId,
                OrchestrationInstanceId: instanceId,
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow
                )
            );
        }

        await _durableTaskClient.ScheduleNewOrchestrationInstanceAsync(new TaskName(nameof(SourceCodeAgent)),
            new SourceCodeAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));

        return instanceId;
    }

    public SourceCodeInput DeserializeInput(string serializedOrchestraionInput)
    {
        return JsonSerializer.Deserialize<SourceCodeAgentInput>(serializedOrchestraionInput).ThrowIfNull().Input;
    }
}
