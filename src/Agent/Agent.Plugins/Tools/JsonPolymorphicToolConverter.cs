// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace Agent.Plugins.Tools
{
    public static class JsonToolConverter
    {
        private static readonly Dictionary<string, Type> _typeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["KustoTool"] = typeof(KustoToolDefinition),

            // Add other supported types
        };

        public static bool TryResolve(string typeName, [NotNullWhen(true)] out Type? concreteType) =>
            _typeMap.TryGetValue(typeName, out concreteType);
    }
}
