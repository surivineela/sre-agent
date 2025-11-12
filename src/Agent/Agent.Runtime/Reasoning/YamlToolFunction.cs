using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Agent.Framework;
using Agent.Plugins.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

public class YamlToolFunction<TContext> : IDeferredToolFunction<TContext> where TContext : class
{
    private readonly IServiceProvider _sp;
    private readonly IEnumerable<Assembly> _assemblies;
    private readonly YamlToolDefinitionBase _toolDef;
    private Guid? _threadId;

    public YamlToolFunction(IServiceProvider sp, IEnumerable<Assembly> assemblies, YamlToolDefinitionBase toolDef)
    {
        _sp = sp;
        _assemblies = assemblies;
        _toolDef = toolDef;
    }

    public MethodInfo? MethodInfo => null;

    public AIFunction GetToolFunction(Guid? threadId = null, Agent<TContext>? agent = null)
    {
        return GetToolFunction(threadId, null, agent);
    }

    public AIFunction GetToolFunction(Guid? threadId, string? agentMode, Agent<TContext>? agent = null)
    {
        _threadId = threadId;
        // For now, YAML tools don't support agent mode, so we ignore the agentMode parameter
        // You can add agent mode support for YAML tools later if needed

        var pluginType = _assemblies
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.GetCustomAttribute<ToolTypeAttribute>()?.Name.Equals(_toolDef.Type, StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new TypeLoadException($"No plugin found for type '{_toolDef.Type}'");

        var pluginMethod = pluginType.GetMethod("Run", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?? throw new MissingMethodException(pluginType.Name, "Run");

        // Create an instance that will handle the execution
        var instance = new YamlToolFunction<TContext>(_sp, _assemblies, _toolDef)
        {
            _threadId = threadId
        };

        // Return our custom AIFunction implementation that handles YAML parameters properly
        return new YamlAwareAIFunction<TContext>(instance, _toolDef);
    }

    public async Task<string?> ExecuteCore(Dictionary<string, object?> parameterValues, CancellationToken cancellationToken)
    {
        var pluginType = _assemblies
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.GetCustomAttribute<ToolTypeAttribute>()?.Name.Equals(_toolDef.Type, StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new TypeLoadException($"No plugin type found for tool type '{_toolDef.Type}'");

        var method = pluginType.GetMethod("Run", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?? throw new MissingMethodException(pluginType.Name, "Run");

        var instance = _sp.GetRequiredService(pluginType);

        var flatArgs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Copy the provided parameters
        foreach (var kvp in parameterValues)
        {
            flatArgs[kvp.Key] = kvp.Value;
        }

        foreach (var parameter in _toolDef.Parameters)
        {
            // prefill missing arguments with YAML-defined defaults so downstream normalizers/validators see them
            if (parameter.Value is not null && !flatArgs.ContainsKey(parameter.Name))
            {
                flatArgs[parameter.Name] = parameter.Value;
            }
        }

        ValidateAndNormalizeParameters(flatArgs);

        flatArgs["threadId"] = _threadId;
        flatArgs["toolName"] = _toolDef.Name;

        var transformer = instance as IToolArgumentTransformer ?? new DeclarativeArgumentTransformer();
        var transformedArgs = transformer.TransformArguments(method, flatArgs, _toolDef);
        var invokeArgs = transformedArgs?.Select(arg => arg ?? (object)string.Empty).ToArray() ?? new object[0];

        if (instance is IYamlToolAware aware)
        {
            aware.SetToolDefinition(_toolDef);
        }
        try
        {
            var result = method.Invoke(instance, invokeArgs);

            if (result is Task<string> taskStr) return await taskStr;
            if (result is Task task) { await task; return null; }

            return result?.ToString();
        }
        catch (TargetParameterCountException ex)
        {
            var expectedParams = method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}");
            var providedArgTypes = invokeArgs.Select(a => a?.GetType().Name ?? "null");

            var errorMessage = $"""
        Error invoking method '{method.Name}' on type '{pluginType.Name}'. Parameter count mismatch.
        - Expected Parameters ({method.GetParameters().Length}): {string.Join(", ", expectedParams)}
        - Provided Arguments ({invokeArgs.Length}): [{string.Join(", ", providedArgTypes)}]
        """;

            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    public static Dictionary<string, object?> ConvertArgsToDictionary(JsonElement jsonArgs)
    {
        if (jsonArgs.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Expected a JSON object for arguments.");

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in jsonArgs.EnumerateObject())
        {
            result[prop.Name] = ConvertJsonValue(prop.Value);
        }

        return result;
    }

    private static object? ConvertJsonValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var l) ? l : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };

    private void ValidateAndNormalizeParameters(Dictionary<string, object?> flatArgs)
    {
        // Enforce YAML validation contracts: pick parameters with validation metadata, normalize string inputs,
        // and reject values that fail regex checks so downstream plugin code only sees sanitized arguments.
        foreach (var parameter in _toolDef.Parameters)
        {
            if (parameter.Validation is null)
            {
                continue;
            }

            if (!flatArgs.TryGetValue(parameter.Name, out var rawValue))
            {
                continue;
            }

            if (rawValue is JsonElement jsonElement)
            {
                rawValue = ConvertJsonValue(jsonElement);
            }

            if (!string.Equals(parameter.Type, "string", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stringValue = rawValue?.ToString();
            if (stringValue is null)
            {
                continue;
            }

            var normalizedValue = ApplyNormalizers(stringValue, parameter.Validation.Normalize);

            if (parameter.Validation.HasRegex)
            {
                var regex = parameter.Validation.BuildRegex();
                if (regex is not null && !regex.IsMatch(normalizedValue))
                {
                    var message = parameter.Validation.ErrorMessage ?? $"Parameter '{parameter.Name}' failed validation.";
                    throw new ValidationException(message);
                }
            }

            flatArgs[parameter.Name] = normalizedValue;
        }
    }

    private static string ApplyNormalizers(string value, IReadOnlyCollection<string>? normalizers)
    {
        if (normalizers is null || normalizers.Count == 0)
        {
            return value;
        }

        var result = value;

        foreach (var normalizer in normalizers)
        {
            if (string.IsNullOrWhiteSpace(normalizer))
            {
                continue;
            }

            switch (normalizer.Trim().ToLowerInvariant())
            {
                case "trim":
                    result = result.Trim();
                    break;
                case "trimstart":
                    result = result.TrimStart();
                    break;
                case "trimend":
                    result = result.TrimEnd();
                    break;
                case "lower":
                case "lowerinvariant":
                    result = result.ToLowerInvariant();
                    break;
                case "upper":
                case "upperinvariant":
                    result = result.ToUpperInvariant();
                    break;
            }
        }

        return result;
    }

    public string GetPluginCategory()
    {
        return _toolDef.Type;
    }

    public string GetPluginResourceType()
    {
        return _toolDef.Type;
    }

    public string GetPluginName()
    {
        return _toolDef.Name;
    }
}

/// <summary>
/// A wrapper AIFunction that incorporates YAML parameter descriptions into the function schema
/// </summary>
internal class YamlAwareAIFunction<TContext> : AIFunction where TContext : class
{
    private readonly YamlToolFunction<TContext> _yamlFunction;
    private readonly YamlToolDefinitionBase _toolDef;
    private readonly JsonElement _customSchema;

    public YamlAwareAIFunction(YamlToolFunction<TContext> yamlFunction, YamlToolDefinitionBase toolDef)
    {
        _yamlFunction = yamlFunction;
        _toolDef = toolDef;
        _customSchema = CreateCustomSchema();
    }

    public override string Name => _toolDef.Name;
    public override string Description => _toolDef.Description;
    public override JsonElement JsonSchema => _customSchema;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => new Dictionary<string, object?>();

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // Convert AIFunctionArguments to Dictionary<string, object?>
        var argsDict = new Dictionary<string, object?>();
        foreach (var arg in arguments)
        {
            argsDict[arg.Key] = arg.Value;
        }

        return await _yamlFunction.ExecuteCore(argsDict, cancellationToken);
    }

    private JsonElement CreateCustomSchema()
    {
        // Create a schema that includes the YAML parameter descriptions
        var properties = new Dictionary<string, object?>();
        var required = new List<string>();

        foreach (var param in _toolDef.Parameters)
        {
            var paramType = param.Type switch
            {
                "int" => "integer",
                "bool" => "boolean",
                "double" => "number",
                _ => "string"
            };

            var propertyDefinition = new Dictionary<string, object?>
            {
                ["type"] = paramType,
                ["description"] = param.Description ?? $"Parameter {param.Name}"
            };

            if (param.Validation?.HasRegex == true && !string.IsNullOrWhiteSpace(param.Validation.Regex))
            {
                propertyDefinition["pattern"] = param.Validation.Regex;
            }

            properties[param.Name] = propertyDefinition;

            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        var schema = new
        {
            type = "object",
            properties = properties,
            required = required.ToArray()
        };

        var json = JsonSerializer.Serialize(schema);
        return JsonDocument.Parse(json).RootElement;
    }
}
