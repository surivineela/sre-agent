// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Runtime.SubAgents;

namespace Agent.Core.Helpers
{
    public static class SubAgentDiscovery
    {
        private static HashSet<string> _internalAgents =
        [
            nameof(MCPAgent),
            nameof(MCPMetaAgent)
        ];
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
                           typeof(SubAgent).IsAssignableFrom(t) &&
                           !_internalAgents.Contains(t.Name))
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

