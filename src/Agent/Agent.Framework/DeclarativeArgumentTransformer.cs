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
                var convertedValue = SafeConvertType(kvp.Value, valueType);
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
                argument = SafeConvertType(directVal, paramInfo.ParameterType);
            }
            else
            {
                var match = toolDef.Parameters.FirstOrDefault(p =>
                    p.MapTo?.Equals(name, StringComparison.OrdinalIgnoreCase) == true &&
                    (p.Target?.Equals("direct", StringComparison.OrdinalIgnoreCase) == true ||
                     p.Target?.StartsWith("direct", StringComparison.OrdinalIgnoreCase) == true));

                if (match != null && flatArgs.TryGetValue(match.Name, out var val))
                {
                    argument = SafeConvertType(val, paramInfo.ParameterType);
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
        // Handle Dictionary types specially - return empty dictionary
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            return Activator.CreateInstance(type);
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static object? SafeConvertType(object? value, Type targetType)
    {
        if (value == null)
        {
            // Handle null values appropriately
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
            {
                // For non-nullable value types, return default value
                return Activator.CreateInstance(targetType);
            }
            return null;
        }

        // If types are already compatible, return as-is
        if (targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }
        try
        {
            // Handle JsonElement specifically - check for JsonElement type name as well in case of type system issues
            if (value is System.Text.Json.JsonElement jsonElement ||
                value?.GetType().Name == "JsonElement")
            {
                if (value is System.Text.Json.JsonElement je)
                {
                    return ConvertJsonElementToType(je, targetType);
                }
            }

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                return SafeConvertType(value, underlyingType);
            }

            // Handle string conversion to enum
            if (targetType.IsEnum && value is string stringValue)
            {
                return Enum.Parse(targetType, stringValue, ignoreCase: true);
            }

            // Only use Convert.ChangeType for types that implement IConvertible
            if (value is IConvertible)
            {
                return Convert.ChangeType(value, targetType);
            }

            // For non-IConvertible types, try ToString() conversion if target is string
            if (targetType == typeof(string))
            {
                return value?.ToString() ?? string.Empty;
            }

            // Last resort - return default value
            return GetDefault(targetType);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            // If conversion fails, return default value for the type
            return GetDefault(targetType);
        }
    }

    private static object? ConvertJsonElementToType(System.Text.Json.JsonElement jsonElement, Type targetType)
    {
        try
        {
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                targetType = underlyingType;
            }

            if (targetType == typeof(string))
            {
                return jsonElement.ValueKind == System.Text.Json.JsonValueKind.String
                    ? jsonElement.GetString()
                    : jsonElement.GetRawText();
            }

            if (targetType == typeof(int))
            {
                return jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? jsonElement.GetInt32()
                    : int.TryParse(jsonElement.GetString(), out var intVal) ? intVal : 0;
            }

            if (targetType == typeof(long))
            {
                return jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? jsonElement.GetInt64()
                    : long.TryParse(jsonElement.GetString(), out var longVal) ? longVal : 0L;
            }

            if (targetType == typeof(double))
            {
                return jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? jsonElement.GetDouble()
                    : double.TryParse(jsonElement.GetString(), out var doubleVal) ? doubleVal : 0.0;
            }

            if (targetType == typeof(float))
            {
                return jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? jsonElement.GetSingle()
                    : float.TryParse(jsonElement.GetString(), out var floatVal) ? floatVal : 0.0f;
            }

            if (targetType == typeof(bool))
            {
                return jsonElement.ValueKind == System.Text.Json.JsonValueKind.True ? true :
                       jsonElement.ValueKind == System.Text.Json.JsonValueKind.False ? false :
                       bool.TryParse(jsonElement.GetString(), out var boolVal) ? boolVal : false;
            }

            if (targetType == typeof(DateTime))
            {
                return DateTime.TryParse(jsonElement.GetString(), out var dateVal) ? dateVal : DateTime.MinValue;
            }

            if (targetType.IsEnum)
            {
                var enumString = jsonElement.GetString();
                return enumString != null ? Enum.Parse(targetType, enumString, ignoreCase: true) : GetDefault(targetType);
            }

            // For other types, try to get the raw value and convert
            object? rawValue = jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                System.Text.Json.JsonValueKind.Number => jsonElement.GetDecimal(),
                System.Text.Json.JsonValueKind.True => (object)true,
                System.Text.Json.JsonValueKind.False => (object)false,
                System.Text.Json.JsonValueKind.Null => null,
                _ => jsonElement.GetRawText()
            };

            return rawValue == null ? GetDefault(targetType) : Convert.ChangeType(rawValue, targetType);
        }
        catch
        {
            // Return default value on conversion failure
            return GetDefault(targetType);
        }
    }
}
