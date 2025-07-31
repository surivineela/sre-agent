// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using System.Globalization;
using System.Reflection;
using Agent.Framework;

public class DeclarativeArgumentTransformer : IToolArgumentTransformer
{
    public object?[] TransformArguments(MethodInfo method, Dictionary<string, object?> flatArgs, YamlToolDefinitionBase toolDef)
    {
        var parameters = method.GetParameters();
        var finalArgs = new List<object?>(parameters.Length);

        // Step 1: Group parameters by mapTo and value type
        var groupedByMapTo = new Dictionary<string, (string valueTypeName, Dictionary<string, object?> values)>(StringComparer.OrdinalIgnoreCase);

        foreach (var param in toolDef.Parameters)
        {
            if (!flatArgs.TryGetValue(param.Name, out var value))
                continue;

            var mapTo = param.MapTo ?? throw new InvalidOperationException($"Missing mapTo for '{param.Name}'");
            var targetParts = param.Target?.Split(':') ?? Array.Empty<string>();

            if (targetParts.Length != 3 || !targetParts[0].Equals("dictionary", StringComparison.OrdinalIgnoreCase))
                continue;

            var valueType = targetParts[2];

            if (!groupedByMapTo.TryGetValue(mapTo, out var entry))
            {
                entry = (valueType, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
                groupedByMapTo[mapTo] = entry;
            }

            entry.values[param.Name] = value;
        }

        // Step 2: Convert grouped dictionaries to strongly typed Dictionary<string, T>
        var typedDictionaries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (mapTo, (valueTypeName, rawDict)) in groupedByMapTo)
        {
            var valueType = Type.GetType("System." + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(valueTypeName))
                            ?? throw new InvalidOperationException($"Unknown value type '{valueTypeName}' for '{mapTo}'");

            var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
            var typedDict = (IDictionary)Activator.CreateInstance(dictType)!;

            foreach (var kvp in rawDict)
            {
                var convertedValue = kvp.Value != null ? Convert.ChangeType(kvp.Value, valueType) : null;
                typedDict[kvp.Key] = convertedValue;
            }

            typedDictionaries[mapTo] = typedDict;
        }

        // Step 3: Populate method parameters
        foreach (var paramInfo in parameters)
        {
            var name = paramInfo.Name!;
            object? argument;

            if (typedDictionaries.TryGetValue(name, out var typedDictValue))
            {
                argument = typedDictValue;
            }
            else if (flatArgs.TryGetValue(name, out var directVal))
            {
                argument = Convert.ChangeType(directVal, paramInfo.ParameterType);
            }
            else
            {
                var match = toolDef.Parameters.FirstOrDefault(p =>
                    p.MapTo?.Equals(name, StringComparison.OrdinalIgnoreCase) == true &&
                    (p.Target?.Equals("direct", StringComparison.OrdinalIgnoreCase) == true ||
                     p.Target?.StartsWith("direct", StringComparison.OrdinalIgnoreCase) == true));

                if (match != null && flatArgs.TryGetValue(match.Name, out var val))
                {
                    argument = Convert.ChangeType(val, paramInfo.ParameterType);
                }
                else
                {
                    argument = GetDefault(paramInfo.ParameterType);
                }
            }

            finalArgs.Add(argument);
        }

        return finalArgs.ToArray();
    }

    private static object? GetDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
