// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
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
            "RCAAgent",
            "IncidentAgent"
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
            typeof(IcmPluginDefinition),    
            typeof(ContainerAppRevisionPluginDefinition),
            typeof(KustoPluginDefinition)
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
        string promptFileName = agentName+ "SystemPrompt.txt";
        var path = Path.Combine("..", "FirstPartyAgent.Core", nameof(FirstPartyAgents), "ACA", promptFileName);
        systemPrompt = File.ReadAllText(path);
        if(string.IsNullOrEmpty(systemPrompt))
        {
            throw new InvalidOperationException($"System prompt {promptFileName} not found for the agent {agentName}");
        }
        return systemPrompt;
    }
}
