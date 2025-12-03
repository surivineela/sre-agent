// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Agent.Core.Helpers.JmesPath;

/// <summary>
/// Signature attribute for function parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class SignatureAttribute : Attribute
{
    public string[] Types { get; set; }
    public bool Variadic { get; set; }

    public SignatureAttribute(params string[] types)
    {
        Types = types;
        Variadic = false;
    }
}

/// <summary>
/// Specification for a function including method and signatures.
/// </summary>
internal class FunctionSpec
{
    public System.Reflection.MethodInfo Method { get; set; } = null!;
    public List<SignatureAttribute> Signatures { get; set; } = new();
}

/// <summary>
/// Built-in JMESPath functions.
/// </summary>
public class Functions
{
    // .NET types -> JMESPath types
    // Maps .NET type names to their JMESPath equivalents for type checking
    private static readonly Dictionary<string, string> TypesMap = new()
    {
        { "Boolean", "boolean" },
        { "JsonElement", "unknown" },
        { "String", "string" },
        { "Int32", "number" },
        { "Int64", "number" },
        { "Single", "number" },
        { "Double", "number" },
        { "Decimal", "number" }
    };

    // JMESPath types -> JMESPath types (for subtype checking)
    // Maps JMESPath type names to arrays of allowed subtypes.
    // This allows checking if array elements match the expected subtype.
    private static readonly Dictionary<string, string[]> ReverseTypesMap = new()
    {
        { "boolean", new[] { "boolean" } },
        { "array", new[] { "array" } },
        { "object", new[] { "object" } },
        { "null", new[] { "null" } },
        { "string", new[] { "string" } },
        { "number", new[] { "number" } },
        { "expref", new[] { "expref" } }
    };

    private readonly Dictionary<string, FunctionSpec> _functionTable = new();

    public Functions()
    {
        PopulateFunctionTable();
    }

    private void PopulateFunctionTable()
    {
        var methods = GetType().GetMethods(System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        foreach (var method in methods)
        {
            if (!method.Name.StartsWith("_func_")) continue;

            var signatures = method.GetCustomAttributes(typeof(SignatureAttribute), false)
                .Cast<SignatureAttribute>().ToList();

            if (signatures.Any())
            {
                var functionName = method.Name[6..]; // Remove "_func_" prefix
                _functionTable[functionName] = new FunctionSpec
                {
                    Method = method,
                    Signatures = signatures
                };
            }
        }
    }

    public object? CallFunction(string functionName, List<object?> resolvedArgs, TreeInterpreter? interpreter = null)
    {
        if (!_functionTable.TryGetValue(functionName, out var spec))
        {
            throw new UnknownFunctionException($"Unknown function: {functionName}()");
        }

        var signature = spec.Signatures.FirstOrDefault();
        if (signature != null)
        {
            ValidateArguments(resolvedArgs, signature, functionName);
        }

        // Check if the method needs the interpreter parameter
        var methodParams = spec.Method.GetParameters();
        var needsInterpreter = methodParams.Length > 0 &&
                              methodParams[^1].ParameterType == typeof(TreeInterpreter);

        // Check if the method uses params array (variadic)
        var lastParam = methodParams.LastOrDefault();
        var isVariadic = lastParam != null && lastParam.GetCustomAttributes(typeof(ParamArrayAttribute), false).Any();

        // Convert arguments to match method parameter types
        var args = new List<object?>();

        if (isVariadic && !needsInterpreter)
        {
            // For variadic functions, collect all args into a params array
            var paramsArrayType = lastParam!.ParameterType.GetElementType()!;
            var convertedParams = new List<object?>();

            foreach (var arg in resolvedArgs)
            {
                convertedParams.Add(ConvertArgument(arg, paramsArrayType));
            }

            var paramsArray = Array.CreateInstance(paramsArrayType, convertedParams.Count);
            for (int i = 0; i < convertedParams.Count; i++)
            {
                paramsArray.SetValue(convertedParams[i], i);
            }
            args.Add(paramsArray);
        }
        else
        {
            // Regular function arguments
            for (int i = 0; i < resolvedArgs.Count && i < methodParams.Length; i++)
            {
                var arg = resolvedArgs[i];
                var paramType = methodParams[i].ParameterType;
                args.Add(ConvertArgument(arg, paramType));
            }
        }

        if (needsInterpreter && interpreter != null)
        {
            args.Add(interpreter);
        }

        try
        {
            return spec.Method.Invoke(this, args.ToArray());
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Unwrap reflection exceptions to preserve the original exception type
            throw ex.InnerException;
        }
    }

    private object? ConvertArgument(object? arg, Type targetType)
    {
        if (arg == null) return null;

        if (arg is not JsonElement element)
            return arg;

        // Convert JsonElement to native types as needed
        if (targetType == typeof(string))
        {
            return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        }
        else if (targetType == typeof(int) || targetType == typeof(long) ||
                 targetType == typeof(double) || targetType == typeof(float))
        {
            return element.ValueKind == JsonValueKind.Number ? element.GetDouble() : (object?)null;
        }
        else if (targetType == typeof(bool))
        {
            return (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
                ? element.GetBoolean() : (object?)null;
        }
        else if (targetType == typeof(JsonElement[]))
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element.EnumerateArray().ToArray();
            }
            return null;
        }
        else if (targetType == typeof(JsonElement) || targetType == typeof(object))
        {
            return element;
        }

        return arg;
    }

    private void ValidateArguments(List<object?> args, SignatureAttribute signature, string functionName)
    {
        if (signature.Variadic)
        {
            // For variadic functions, require at least as many args as signature entries
            var requiredCount = signature.Types.Length;
            if (args.Count < requiredCount)
            {
                throw new VariadicArityException(requiredCount, args.Count, functionName);
            }
        }
        else
        {
            var paramCount = signature.Types.Length;
            if (args.Count != paramCount)
            {
                throw new ArityException(paramCount, args.Count, functionName);
            }
        }

        TypeCheck(args, signature, functionName);
    }

    private void TypeCheck(List<object?> actual, SignatureAttribute signature, string functionName)
    {
        for (int i = 0; i < signature.Types.Length && i < actual.Count; i++)
        {
            var allowedTypes = signature.Types[i];
            if (!string.IsNullOrEmpty(allowedTypes))
            {
                TypeCheckSingle(actual[i], allowedTypes.Split(','), functionName);
            }
        }
    }

    private void TypeCheckSingle(object? current, string[] types, string functionName)
    {
        // Type checking involves checking the top level type,
        // and in the case of arrays, potentially checking the types
        // of each element.
        var (allowedTypes, allowedSubtypes) = GetAllowedPyTypes(types);
        // We use GetJmesPathType() rather than direct type checks on purpose.
        // The type model for JMESPath does not map 1-1 with .NET types
        // (booleans are considered integers in some languages for example).
        var actualTypename = GetJmesPathType(current);

        if (!allowedTypes.Contains(actualTypename))
        {
            throw new JmesPathTypeException(functionName, current, actualTypename, types);
        }

        // If we're dealing with an array type, we can have
        // additional restrictions on the type of the array
        // elements (for example a function can require an
        // array of numbers or an array of strings).
        // Arrays are the only types that can have subtypes.
        if (allowedSubtypes.Any() && current is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            SubtypeCheck(element, allowedSubtypes, types, functionName);
        }
    }

    private (List<string>, List<string[]>) GetAllowedPyTypes(string[] types)
    {
        var allowedTypes = new List<string>();
        var allowedSubtypes = new List<string[]>();

        foreach (var t in types)
        {
            var parts = t.Split('-');
            if (parts.Length == 2)
            {
                var type = parts[0];
                var subtype = parts[1];
                allowedTypes.Add(type);
                if (ReverseTypesMap.TryGetValue(subtype, out var subtypes))
                {
                    allowedSubtypes.Add(subtypes);
                }
            }
            else
            {
                allowedTypes.Add(parts[0]);
            }
        }

        return (allowedTypes, allowedSubtypes);
    }

    private void SubtypeCheck(JsonElement array, List<string[]> allowedSubtypes, string[] types, string functionName)
    {
        var arrayList = array.EnumerateArray().ToList();
        if (arrayList.Count == 0)
        {
            return; // Empty arrays are always valid
        }

        if (allowedSubtypes.Count == 1)
        {
            // The easy case, we know up front what type we need to validate.
            var allowed = allowedSubtypes[0];
            foreach (var element in arrayList)
            {
                var actualTypename = GetJmesPathType(element);
                if (!allowed.Contains(actualTypename))
                {
                    throw new JmesPathTypeException(functionName, element, actualTypename, types);
                }
            }
        }
        else if (allowedSubtypes.Count > 1)
        {
            // Dynamic type validation. Based on the first type we see,
            // we validate that the remaining types match.
            var firstType = GetJmesPathType(arrayList[0]);
            string[]? allowed = null;

            foreach (var subtypes in allowedSubtypes)
            {
                if (subtypes.Contains(firstType))
                {
                    allowed = subtypes;
                    break;
                }
            }

            if (allowed == null)
            {
                throw new JmesPathTypeException(functionName, arrayList[0], firstType, types);
            }

            foreach (var element in arrayList)
            {
                var actualTypename = GetJmesPathType(element);
                if (!allowed.Contains(actualTypename))
                {
                    throw new JmesPathTypeException(functionName, element, actualTypename, types);
                }
            }
        }
    }

    private string GetJmesPathType(object? obj)
    {
        if (obj == null) return "null";
        if (obj is ExpressionRef) return "expref";
        if (obj is string) return "string";
        if (obj is bool) return "boolean";
        if (IsNumericType(obj)) return "number";

        if (obj is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True => "boolean",
                JsonValueKind.False => "boolean",
                JsonValueKind.Array => "array",
                JsonValueKind.Object => "object",
                JsonValueKind.Null => "null",
                _ => "unknown"
            };
        }

        return "unknown";
    }

    private bool IsNumericType(object? obj)
    {
        return obj is int or long or float or double or decimal;
    }

    private bool IsNumber(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Number;
    }

    // Built-in functions implementation
    [Signature("number")]
    private object? _func_abs(object? arg)
    {
        return arg switch
        {
            int i => Math.Abs(i),
            long l => Math.Abs(l),
            double d => Math.Abs(d),
            float f => Math.Abs(f),
            decimal dec => Math.Abs(dec),
            JsonElement e when e.ValueKind == JsonValueKind.Number => Math.Abs(e.GetDouble()),
            _ => null
        };
    }

    [Signature("array-number")]
    private object? _func_avg(JsonElement arg)
    {
        if (arg.ValueKind != JsonValueKind.Array) return null;

        var values = arg.EnumerateArray().Select(e => e.GetDouble()).ToList();
        return values.Any() ? values.Average() : null;
    }

    [Signature("", Variadic = true)]
    private object? _func_not_null(params object?[] arguments)
    {
        foreach (var argument in arguments)
        {
            if (argument != null &&
                !(argument is JsonElement e && e.ValueKind == JsonValueKind.Null))
            {
                return argument;
            }
        }
        return null;
    }

    [Signature("")]
    private object? _func_to_array(object? arg)
    {
        if (arg is JsonElement e && e.ValueKind == JsonValueKind.Array)
        {
            return arg;
        }

        // Convert to array
        var arrayDoc = JsonDocument.Parse($"[{JsonSerializer.Serialize(arg)}]");
        return arrayDoc.RootElement.Clone();
    }

    [Signature("")]
    private object? _func_to_string(object? arg)
    {
        if (arg is string s) return s;
        if (arg is JsonElement e && e.ValueKind == JsonValueKind.String)
        {
            return e.GetString();
        }
        return JsonSerializer.Serialize(arg);
    }

    [Signature("")]
    private object? _func_to_number(object? arg)
    {
        if (arg == null) return null;
        if (arg is bool) return null;
        if (IsNumericType(arg)) return arg;

        if (arg is JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Number) return e.GetDouble();
            if (e.ValueKind == JsonValueKind.String)
            {
                var str = e.GetString();
                if (int.TryParse(str, out var i)) return i;
                if (double.TryParse(str, out var d)) return d;
            }
            return null;
        }

        if (arg is string str2)
        {
            if (int.TryParse(str2, out var i)) return i;
            if (double.TryParse(str2, out var d)) return d;
        }

        return null;
    }

    [Signature("array,string", "")]
    private bool _func_contains(object? subject, object? search)
    {
        if (subject is JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in e.EnumerateArray())
                {
                    if (JsonElementEquals(item, search)) return true;
                }
                return false;
            }
            if (e.ValueKind == JsonValueKind.String && search is string searchStr)
            {
                return e.GetString()?.Contains(searchStr) ?? false;
            }
        }

        if (subject is string subjectStr && search is string searchStr2)
        {
            return subjectStr.Contains(searchStr2);
        }

        return false;
    }

    [Signature("string,array,object")]
    private object? _func_length(object? arg)
    {
        if (arg is string s) return s.Length;
        if (arg is JsonElement e)
        {
            return e.ValueKind switch
            {
                JsonValueKind.String => e.GetString()?.Length,
                JsonValueKind.Array => e.GetArrayLength(),
                JsonValueKind.Object => e.EnumerateObject().Count(),
                _ => null
            };
        }
        return null;
    }

    [Signature("string", "string")]
    private bool _func_ends_with(string search, string suffix)
    {
        return search.EndsWith(suffix);
    }

    [Signature("string", "string")]
    private bool _func_starts_with(string search, string prefix)
    {
        return search.StartsWith(prefix);
    }

    [Signature("array,string")]
    private object? _func_reverse(object? arg)
    {
        if (arg is string s)
        {
            return new string(s.Reverse().ToArray());
        }
        if (arg is JsonElement e && e.ValueKind == JsonValueKind.Array)
        {
            var items = e.EnumerateArray().Reverse().ToList();
            var json = JsonSerializer.Serialize(items);
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        return null;
    }

    [Signature("number")]
    private object? _func_ceil(object? arg)
    {
        return arg switch
        {
            double d => Math.Ceiling(d),
            float f => Math.Ceiling(f),
            decimal dec => Math.Ceiling(dec),
            JsonElement e when e.ValueKind == JsonValueKind.Number => Math.Ceiling(e.GetDouble()),
            _ => arg
        };
    }

    [Signature("number")]
    private object? _func_floor(object? arg)
    {
        return arg switch
        {
            double d => Math.Floor(d),
            float f => Math.Floor(f),
            decimal dec => Math.Floor(dec),
            JsonElement e when e.ValueKind == JsonValueKind.Number => Math.Floor(e.GetDouble()),
            _ => arg
        };
    }

    [Signature("string", "array-string")]
    private object? _func_join(string separator, JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;
        var strings = array.EnumerateArray().Select(e => e.GetString() ?? "");
        return string.Join(separator, strings);
    }

    [Signature("expref", "array")]
    private object? _func_map(ExpressionRef expref, JsonElement array, TreeInterpreter interpreter)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;

        var result = new List<object?>();
        foreach (var element in array.EnumerateArray())
        {
            result.Add(interpreter.Visit(expref.Expression, element));
        }

        var json = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Signature("array-number,array-string")]
    private object? _func_max(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;

        var items = array.EnumerateArray().ToList();
        if (!items.Any()) return null;

        if (items[0].ValueKind == JsonValueKind.Number)
        {
            return items.Max(e => e.GetDouble());
        }
        if (items[0].ValueKind == JsonValueKind.String)
        {
            return items.Max(e => e.GetString());
        }

        return null;
    }

    [Signature("object", Variadic = true)]
    private object? _func_merge(params JsonElement[] arguments)
    {
        var merged = new Dictionary<string, JsonElement>();

        foreach (var arg in arguments)
        {
            if (arg.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in arg.EnumerateObject())
                {
                    merged[prop.Name] = prop.Value;
                }
            }
        }

        var json = JsonSerializer.Serialize(merged);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Signature("array-number,array-string")]
    private object? _func_min(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;

        var items = array.EnumerateArray().ToList();
        if (!items.Any()) return null;

        if (items[0].ValueKind == JsonValueKind.Number)
        {
            return items.Min(e => e.GetDouble());
        }
        if (items[0].ValueKind == JsonValueKind.String)
        {
            return items.Min(e => e.GetString());
        }

        return null;
    }

    [Signature("array-string,array-number")]
    private object? _func_sort(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;

        var items = array.EnumerateArray().ToList();
        if (!items.Any()) return array;

        if (items[0].ValueKind == JsonValueKind.Number)
        {
            var sorted = items.OrderBy(e => e.GetDouble()).ToList();
            var json = JsonSerializer.Serialize(sorted);
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        if (items[0].ValueKind == JsonValueKind.String)
        {
            var sorted = items.OrderBy(e => e.GetString()).ToList();
            var json = JsonSerializer.Serialize(sorted);
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        return array;
    }

    [Signature("array-number")]
    private object? _func_sum(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;
        return array.EnumerateArray().Sum(e => e.GetDouble());
    }

    [Signature("object")]
    private object? _func_keys(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;

        var keys = obj.EnumerateObject().Select(p => p.Name).ToList();
        var json = JsonSerializer.Serialize(keys);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Signature("object")]
    private object? _func_values(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;

        var values = obj.EnumerateObject().Select(p => p.Value).ToList();
        var json = JsonSerializer.Serialize(values);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Signature("")]
    private string _func_type(object? arg)
    {
        return GetJmesPathType(arg);
    }

    [Signature("array", "expref")]
    private object? _func_sort_by(JsonElement array, ExpressionRef expref, TreeInterpreter interpreter)
    {
        if (array.ValueKind != JsonValueKind.Array) return array;

        var items = array.EnumerateArray().ToList();
        if (!items.Any()) return array;

        // sort_by allows for the expref to be either a number or
        // a string, so we have some special logic to handle this.
        // We evaluate the first array element and verify that it's
        // either a string or a number. We then validate that type,
        // which requires that remaining array elements resolve to
        // the same type as the first element.
        var firstResult = interpreter.Visit(expref.Expression, items[0]);
        var requiredType = GetJmesPathType(firstResult);
        if (requiredType != "number" && requiredType != "string")
            throw new JmesPathTypeException("sort_by", firstResult, requiredType, new[] { "number", "string" });

        // Validate and extract sort keys for all elements
        var sortKeys = new List<IComparable>();
        foreach (var item in items)
        {
            var result = interpreter.Visit(expref.Expression, item);
            var actualType = GetJmesPathType(result);
            if (actualType != requiredType)
                throw new JmesPathTypeException("sort_by", result, actualType, new[] { requiredType });

            if (requiredType == "number")
            {
                if (result is JsonElement e && e.ValueKind == JsonValueKind.Number)
                    sortKeys.Add(e.GetDouble());
                else if (IsNumericType(result))
                    sortKeys.Add(Convert.ToDouble(result));
                else
                    throw new JmesPathTypeException("sort_by", result, actualType, new[] { requiredType });
            }
            else // string
            {
                if (result is JsonElement e && e.ValueKind == JsonValueKind.String)
                    sortKeys.Add(e.GetString()!);
                else if (result is string s)
                    sortKeys.Add(s);
                else
                    throw new JmesPathTypeException("sort_by", result, actualType, new[] { requiredType });
            }
        }

        // Sort using validated keys
        var indexed = items.Select((item, index) => new { item, key = sortKeys[index] }).ToList();
        var sorted = indexed.OrderBy(x => x.key).Select(x => x.item).ToList();

        var json = JsonSerializer.Serialize(sorted);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
    [Signature("array", "expref")]
    private object? _func_min_by(JsonElement array, ExpressionRef expref, TreeInterpreter interpreter)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;

        var items = array.EnumerateArray().ToList();
        if (!items.Any()) return null;

        // Validate all evaluated values are number or string
        var keys = new List<IComparable>();
        foreach (var item in items)
        {
            var result = interpreter.Visit(expref.Expression, item);
            var actualType = GetJmesPathType(result);
            if (actualType != "number" && actualType != "string")
                throw new JmesPathTypeException("min_by", result, actualType, new[] { "number", "string" });

            if (actualType == "number")
            {
                if (result is JsonElement e && e.ValueKind == JsonValueKind.Number)
                    keys.Add(e.GetDouble());
                else if (IsNumericType(result))
                    keys.Add(Convert.ToDouble(result));
                else
                    throw new JmesPathTypeException("min_by", result, actualType, new[] { "number", "string" });
            }
            else // string
            {
                if (result is JsonElement e && e.ValueKind == JsonValueKind.String)
                    keys.Add(e.GetString()!);
                else if (result is string s)
                    keys.Add(s);
                else
                    throw new JmesPathTypeException("min_by", result, actualType, new[] { "number", "string" });
            }
        }

        var minIndex = keys.Select((key, index) => new { key, index })
                          .MinBy(x => x.key)!.index;
        return items[minIndex];
    }

    [Signature("array", "expref")]
    private object? _func_max_by(JsonElement array, ExpressionRef expref, TreeInterpreter interpreter)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;

        var items = array.EnumerateArray().ToList();
        if (!items.Any()) return null;

        // Validate all evaluated values are number or string
        var keys = new List<IComparable>();
        foreach (var item in items)
        {
            var result = interpreter.Visit(expref.Expression, item);
            var actualType = GetJmesPathType(result);
            if (actualType != "number" && actualType != "string")
                throw new JmesPathTypeException("max_by", result, actualType, new[] { "number", "string" });

            if (actualType == "number")
            {
                if (result is JsonElement e && e.ValueKind == JsonValueKind.Number)
                    keys.Add(e.GetDouble());
                else if (IsNumericType(result))
                    keys.Add(Convert.ToDouble(result));
                else
                    throw new JmesPathTypeException("max_by", result, actualType, new[] { "number", "string" });
            }
            else // string
            {
                if (result is JsonElement e && e.ValueKind == JsonValueKind.String)
                    keys.Add(e.GetString()!);
                else if (result is string s)
                    keys.Add(s);
                else
                    throw new JmesPathTypeException("max_by", result, actualType, new[] { "number", "string" });
            }
        }

        var maxIndex = keys.Select((key, index) => new { key, index })
                          .MaxBy(x => x.key)!.index;
        return items[maxIndex];
    }

    private bool JsonElementEquals(JsonElement a, object? b)
    {
        if (b is JsonElement bElement)
        {
            return JsonSerializer.Serialize(a) == JsonSerializer.Serialize(bElement);
        }
        if (b is string s && a.ValueKind == JsonValueKind.String)
        {
            return a.GetString() == s;
        }
        if (IsNumericType(b) && a.ValueKind == JsonValueKind.Number)
        {
            return Math.Abs(a.GetDouble() - Convert.ToDouble(b)) < 0.0001;
        }
        if (b is bool bBool && (a.ValueKind == JsonValueKind.True || a.ValueKind == JsonValueKind.False))
        {
            return a.GetBoolean() == bBool;
        }
        return false;
    }
}

/// <summary>
/// Represents an expression reference (for functions like map, sort_by, etc.)
/// </summary>
public class ExpressionRef
{
    public AstNode Expression { get; }
    public TreeInterpreter Interpreter { get; }

    public ExpressionRef(AstNode expression, TreeInterpreter interpreter)
    {
        Expression = expression;
        Interpreter = interpreter;
    }
}
