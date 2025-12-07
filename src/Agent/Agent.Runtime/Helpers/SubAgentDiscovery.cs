// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Runtime.Helpers;

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
                    return [];
                }
            })
            .Where(t => !t.IsAbstract &&
                       t.IsClass &&
                       typeof(SubAgent).IsAssignableFrom(t))
            .Distinct();
    }
}

