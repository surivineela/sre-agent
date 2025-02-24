using Agent.Core.Models;
using System.Reflection;

namespace Agent.Core.Helpers
{
    public static class SubAgentDiscovery
    {
        public static IEnumerable<Type> DiscoverSubAgentTypes()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("Agent") == true);

            return assemblies
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => !t.IsAbstract &&
                           t.IsClass &&
                           typeof(SubAgent).IsAssignableFrom(t))
                .Distinct();
        }

        public static string GeneratePathFromAgentType(Type agentType)
        {
            var name = agentType.Name.ToLowerInvariant();
            if (name.EndsWith("agent"))
            {
                name = name[..^5];
            }
            return $"/{name}";
        }
    }
}
