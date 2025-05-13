// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent
{
    public class HelloWorldAgentFactory
    {
        
        private readonly IHelloWorldPlugin _helloWorldPlugin;
        private readonly IKustoPlugin _kustoPlugin;
        private readonly AgentToolsRegistry _toolsRegistry;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadOrchestrationManager _mappingManager;
        public const string OrchestrationInstanceIdPrefix = nameof(HelloWorldAgentInput);
        public HelloWorldAgentFactory(
            IHelloWorldPlugin helloWorldPlugin,
            IThreadOrchestrationManager mappingManager,
            IToolsRepository toolsRepository,
            DurableTaskClient durableTaskClient
            )
            
        {
            _helloWorldPlugin = helloWorldPlugin;
            _toolsRegistry = new AgentToolsRegistry();

            
            
            _toolsRegistry.RegisterPlugin<HelloWorldPluginDefinition>();
        }

        public async Task<string> StartOrchestration(
           HelloWorldAgentActivityInput input,
           Guid threadId)
        {
            var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

            await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

            await _durableTaskClient.ScheduleNewHelloWorldAgentInstanceAsync(
                new HelloWorldAgentInput(
                    Input: input,
                    ToolSignatures: _toolsRegistry.ToolSignatures,
                    ThreadId: threadId),
                new StartOrchestrationOptions(InstanceId: instanceId));

            return instanceId;
        }

        public HelloWorldAgentActivityInput DeserializeInput(string serializedOrchestrationInput)
        {
            return JsonSerializer.Deserialize<HelloWorldAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
        }
    }
}
