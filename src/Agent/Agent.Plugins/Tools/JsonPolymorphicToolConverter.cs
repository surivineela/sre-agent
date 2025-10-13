// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.Diagnostics.CodeAnalysis;
using Agent.Data.Tools;

namespace Agent.Plugins.Tools
{
    public static class JsonPolymorphicToolConverter
    {
        private static readonly Dictionary<string, Type> _typeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["KustoTool"] = typeof(KustoToolDefinition),
            // ["LinkTool"]= typeof(LinkToolDefinition),  // Note: This is not a valid tool type right now, commenting out for now
            // Add other supported types when ready
        };

        public static bool TryResolve(string typeName, [NotNullWhen(true)] out Type? concreteType) =>
            _typeMap.TryGetValue(typeName, out concreteType);
    }
}
