using System.Reflection;

namespace Agent.Core.Helpers
{
    public static class TypeReflectionHelpers
    {
        /// <summary>
        /// Returns all types in the given assembly that derive directly from the given
        /// generic type base.
        /// </summary>
        /// <param name="fromAssembly">The assembly to search for derived types</param>
        /// <param name="genericBase">The base type to search for. Note that in order to
        /// support generic derivations, you should use the comma notation for the type
        /// params, like this: typeof(MyClass<,,,>)</param>
        /// <returns></returns>
        public static IEnumerable<Type> GetClassesDerivedFromGeneric(Assembly fromAssembly, Type genericBaseType)
        {
            var genericBase = genericBaseType.GetGenericTypeDefinition();
            foreach (var type in fromAssembly.GetTypes())
            {
                // Note: This only looks at a single-level derivation; it will fail to find any
                // sub-agents that are derived from other sub-agents.
                if (type.BaseType != null
                    && type.BaseType.IsGenericType
                    && type.BaseType.GetGenericTypeDefinition() == genericBase)
                {
                    yield return type;
                }
            }
        }
    }
}
