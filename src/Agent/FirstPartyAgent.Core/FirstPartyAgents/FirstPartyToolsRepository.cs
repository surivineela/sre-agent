using Agent.Plugins.Definitions;
using Agent.Plugins;
using Agent.Runtime.SubAgents;
using Agent.Runtime.MetaAgent.Interfaces;
using System.Reflection;

namespace FirstPartyAgent.Core.FirstPartyAgents;
public class FirstPartyToolsRepository : ToolsRepository
{
    private readonly IFirstPartySubAgentsFactory _firstPartySubAgentsFactory;

    public FirstPartyToolsRepository(IServiceProvider sp, IFirstPartySubAgentsFactory firstPartySubAgentsFactory) : base(sp, false)
    {
        _firstPartySubAgentsFactory = firstPartySubAgentsFactory;
        RegisterFirstPartyPlugins();
    }

    private void RegisterFirstPartyPlugins()
    {
        RegisterPlugin<ControlFlowPluginDefinition>();
        RegisterPlugin<TimePluginDefinition>();
        var firstPartySubAgentPlugins = _firstPartySubAgentsFactory.GetRequiredPluginDefinitionTypes();
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
