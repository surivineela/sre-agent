// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Models;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;

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


        public static List<AITool> GetSubAgentTools(Guid threadGuid, Assembly subAgentsAssembly, IServiceProvider serviceProvider)
        {

            // DEPRACTED DO NOT USE SimpleResourceSubAgentPluginBase
            List<AITool> subAgentAItools = [];
            // Get all instances of background-scanning subagents and register their methods
            var subClasses = TypeReflectionHelpers.GetClassesDerivedFromGeneric(
                subAgentsAssembly,
                typeof(SimpleResourceSubAgentPluginBase<,,,,>)
            );
            foreach (var type in subClasses)
            {
                // Instantiate the type using DI
                var instance = serviceProvider.GetService(type);
                if (instance is null)
                {
                    continue;
                }

                // Set the context
                var prop = type.GetProperty("ThreadId", BindingFlags.Public | BindingFlags.Instance);
                if (prop == null)
                {
                    throw new InvalidOperationException($"Property 'ThreadId' not found on plugin '{type.Name}'");
                }
                prop.SetValue(instance, threadGuid);

                // Get a handle to its methods, and register them in the tools
                var listWorkflowsAsync = type.GetMethod("ListWorkflowsAsync", BindingFlags.Public | BindingFlags.Instance);
                var startAgentAsync = type.GetMethod("StartAgentAsync", BindingFlags.Public | BindingFlags.Instance);
                subAgentAItools.Add(AIFunctionFactory.Create(listWorkflowsAsync, instance));
                subAgentAItools.Add(AIFunctionFactory.Create(startAgentAsync, instance));
            }

            //Best pratice using Attributes to register Workflow Classes , to avoid calling the last workflow registered for any reasoning 
            var allTypes = subAgentsAssembly.GetTypes();
            var extraTypes = allTypes
                .Where(t =>
                    t.IsClass &&
                    !t.IsAbstract &&
                    t.GetCustomAttribute<WorkflowClassAttribute>() != null)
                .ToList();

            foreach (var extraType in extraTypes)
            {
                var extraInstance = serviceProvider.GetService(extraType);
                if (extraInstance == null) continue;

                SetThreadIdIfExists(extraType, extraInstance, threadGuid);

                var methods = GetWorkflowFunctions(extraType);
                foreach (var method in methods)
                {
                    subAgentAItools.Add(AIFunctionFactory.Create(method, extraInstance));
                }
            }
            return subAgentAItools;
            
        }
        private static IEnumerable<MethodInfo> GetWorkflowFunctions(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                       .Where(m => m.GetCustomAttribute<WorkflowFunctionAttribute>() != null);
        }

        private static void SetThreadIdIfExists(Type type, object instance, Guid threadId)
        {
            var prop = type.GetProperty("ThreadId", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(instance, threadId);
            }
        }
    }
}

