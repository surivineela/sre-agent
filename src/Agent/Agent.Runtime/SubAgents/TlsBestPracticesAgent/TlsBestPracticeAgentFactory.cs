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
    private readonly AgentToolsRegistry _toolsRegistry = new AgentToolsRegistry();
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(TlsBestPracticesAgent);

    public TlsBestPracticeAgentFactory(
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
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
        ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";
        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);

        await _durableTaskClient.ScheduleNewTlsBestPracticesAgentInstanceAsync(
            new TlsBestPracticesAgentInput(
                Input: input,
                ToolSignatures: _toolsRegistry.ToolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: instanceId));

        return instanceId;
    }

    public TlsBestPracticesInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<TlsBestPracticesAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}

