// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Agent.Core.Models;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.TlsBestPractices;


// [Export]
public sealed class TlsBestPracticeAgentFactory
{
    private readonly AgentToolsRegistry _toolsRegistry;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(TlsBestPracticesAgent);

    public TlsBestPracticeAgentFactory(
        IToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        _toolsRegistry = new AgentToolsRegistry(toolsRepository);

        _toolsRegistry.RegisterTool<MetricsPluginDefinition>(x => x.GetSuccessfulRequestVolumeAsync);
        _toolsRegistry.RegisterTool<ArmPluginDefinition>(x => x.SetMinimumTlsVersion);
        _toolsRegistry.RegisterPlugin<RecordActionsPluginDefinition>();
        _toolsRegistry.RegisterPlugin<ControlFlowPluginDefinition>();
        _toolsRegistry.RegisterPlugin<ApprovalPluginDefinition>();

        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        TlsBestPracticesInput input,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

        await _durableTaskClient.ScheduleNewTlsBestPracticesAgentInstanceAsync(
            new TlsBestPracticesAgentInput(
                Input: input,
                ToolSignatures: _toolsRegistry.ToolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));

        return instanceId;
    }

    public TlsBestPracticesInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<TlsBestPracticesAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}

