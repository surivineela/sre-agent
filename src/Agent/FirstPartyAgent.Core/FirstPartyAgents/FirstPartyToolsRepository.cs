using System.Reflection;
using Agent.Plugins;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.SubAgents;

namespace FirstPartyAgent.Core.FirstPartyAgents;
public class FirstPartyToolsRepository : ToolsRepository
{
    private readonly IAgentsFactory _agentsFactory;

    public FirstPartyToolsRepository(IServiceProvider sp, IAgentsFactory agentsFactory) : base(sp, false)
    {
        _agentsFactory = agentsFactory;
        RegisterFirstPartyPlugins();
    }

    private void RegisterFirstPartyPlugins()
    {
        RegisterPlugin<ControlFlowPluginDefinition>();
        RegisterPlugin<TimePluginDefinition>();
        RegisterPlugin<ChartPluginDefinition>();
        var firstPartySubAgentPlugins = _agentsFactory.GetRequiredSubAgentPluginDefinitionTypes();
        foreach (var pluginType in firstPartySubAgentPlugins)
        {
            // Use reflection to call the generic RegisterPlugin<T>() method
            var method = typeof(ToolsRepository).GetMethod(nameof(RegisterPlugin), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var genericMethod = method?.MakeGenericMethod(pluginType);

            if (genericMethod == null)
            {
                throw new InvalidOperationException($"Failed to create generic method for '{pluginType.Name}'.");
            }

            // Invoke the generic method
            genericMethod.Invoke(this, null);
        }
    }

}
