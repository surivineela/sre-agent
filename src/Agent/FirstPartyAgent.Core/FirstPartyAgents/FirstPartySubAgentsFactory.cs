// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Plugins;
using Agent.Runtime.MetaAgent.Interfaces;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Plugins.Definitions;

namespace FirstPartyAgent.Core.FirstPartyAgents;
public class FirstPartySubAgentsFactory : IFirstPartySubAgentsFactory
{
    private readonly List<string> _agentNames;

    public FirstPartySubAgentsFactory()
    {
        _agentNames = new List<string>
        {
            "RCAAgent"
        };
    }

    public List<Type> GetRequiredPluginDefinitionTypes()
    {
        // TODO: make it generic
        var types = new List<Type>
        {
            typeof(HelloWorldPluginDefinition),
            //Plugins requires by quota agent.
            //TODO: going to make it read from env variable
            typeof(ContainerAppsPluginDefinition),
            typeof(IcmPluginDefinition)
        };
        return types;
    }

    public bool IsFirstPartyAgent()
    {
        return IsFirstPartyAgent(GetAgentName());
    }

    private bool IsFirstPartyAgent(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        return _agentNames.Contains(name);
    }

    private string GetAgentName()
    {
        return Environment.GetEnvironmentVariable("AGENT_NAME") ?? string.Empty;
    }

    public Assembly GetSubAgentsAssembly()
    {
        return typeof(FirstPartySubAgentsFactory).Assembly;
    }

    public string GetSystemPrompt()
    {
        var agentName = GetAgentName();
        string? systemPrompt = null;
        if (string.Equals(agentName, "RCAAgent", StringComparison.InvariantCultureIgnoreCase))
        {
            var path = Path.Combine("..", "FirstPartyAgent.Core", nameof(FirstPartyAgents), "ACA", "RCAAgentSystemPrompt.txt");
            systemPrompt = File.ReadAllText(path);
        }
        if(string.IsNullOrEmpty(systemPrompt))
        {
            throw new InvalidOperationException("System prompt not found for the agent");
        }
        return systemPrompt;
    }
}
