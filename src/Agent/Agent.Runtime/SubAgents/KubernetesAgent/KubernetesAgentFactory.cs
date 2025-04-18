// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.KubernetesAgent;

// [Export]
public sealed class KubernetesAgentFactory
{
    private readonly AgentToolsRegistry _toolsRegistry = new AgentToolsRegistry();
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(KubernetesAgent);

    public KubernetesAgentFactory(
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        _toolsRegistry.RegisterPlugin<TimePluginDefinition>();
        _toolsRegistry.RegisterPlugin<KubePluginDefinition>();
        _toolsRegistry.RegisterPlugin<ChartPluginDefinition>();
        _toolsRegistry.RegisterPlugin<RecordActionsPluginDefinition>();
        _toolsRegistry.RegisterPlugin<ControlFlowPluginDefinition>();
        _toolsRegistry.RegisterPlugin<ApprovalPluginDefinition>();

        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.VisualizeAKSMicroserviceTopology);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.GetResourceBasicProperties);

        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        string input,
        ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{context.ThreadId}-{DateTime.Now:yyyyMMdd-HHmmss}";

        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);
        return await _durableTaskClient.ScheduleNewKubernetesAgentInstanceAsync(
            new KubernetesAgentInput(
                Input: input,
                ToolSignatures: _toolsRegistry.ToolSignatures,
                context),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<KubernetesAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
