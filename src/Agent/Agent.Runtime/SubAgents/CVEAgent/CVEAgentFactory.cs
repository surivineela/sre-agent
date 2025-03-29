using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Agent.Core.Models;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;

namespace Agent.Runtime.SubAgents.CVEAgent;

// [Export]
public sealed class CVEAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(CVEAgent);

    public CVEAgentFactory(
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient,
        IGithubIssuePlugin githubIssuePlugin)
    {
        var toolSignatures = new List<string>();

        var githubIssuePluginDefinition = new GitHubIssuePluginDefinition(githubIssuePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => githubIssuePluginDefinition.FetchGithubSecurityDependabotAlerts));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        CVEInput input,
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

        await _durableTaskClient.ScheduleNewOrchestrationInstanceAsync(new TaskName(nameof(CVEAgent)),
            new CVEAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));

        return instanceId;
    }

    public CVEInput DeserializeInput(string serializedOrchestraionInput)
    {
        return JsonSerializer.Deserialize<CVEAgentInput>(serializedOrchestraionInput).ThrowIfNull().Input;
    }
}
