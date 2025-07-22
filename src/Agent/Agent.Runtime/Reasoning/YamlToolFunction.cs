using System.Reflection;
using System.Text.Json;
using Agent.Framework;
using Agent.Plugins.Tools;
using Agent.Runtime.Reasoning.Models;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

public class YamlToolFunction<TContext> : IDeferredToolFunction where TContext : class
{
    private readonly IServiceProvider _sp;
    private readonly IEnumerable<Assembly> _assemblies;
    private readonly YamlToolDefinitionBase _toolDef;
    private AIFunctionArguments? _args;
    private Guid? _threadId;

    public YamlToolFunction(IServiceProvider sp, IEnumerable<Assembly> assemblies, YamlToolDefinitionBase toolDef)
    {
        _sp = sp;
        _assemblies = assemblies;
        _toolDef = toolDef;
    }

    public AIFunction GetToolFunction(Guid? threadId = null)
    {
        _threadId = threadId;

        var pluginType = _assemblies
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.GetCustomAttribute<ToolTypeAttribute>()?.Name.Equals(_toolDef.Type, StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new TypeLoadException($"No plugin found for type '{_toolDef.Type}'");

        var pluginMethod = pluginType.GetMethod("Run", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?? throw new MissingMethodException(pluginType.Name, "Run");

        var paramType = ToolParameterTypeGenerator.Create(_toolDef);

        var wrapperMethod = typeof(YamlToolFunction<TContext>)
            .GetMethod(nameof(ExecuteWrapper), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(paramType);

        return AIFunctionFactory.Create(wrapperMethod,
            createInstanceFunc: args => new YamlToolFunction<TContext>(_sp, _assemblies, _toolDef)
            {
                _args = args,
                _threadId = threadId
            },
            new AIFunctionFactoryOptions
            {
                Name = _toolDef.Name,
                Description = _toolDef.Description,
                ConfigureParameterBinding = param => new AIFunctionFactoryOptions.ParameterBindingOptions
                {
                    BindParameter = (_, args) =>
                    {
                        var instance = Activator.CreateInstance(paramType)!;
                        foreach (var p in _toolDef.Parameters)
                        {
                            if (args.TryGetValue(p.Name, out var value) && value != null)
                            {
                                var prop = paramType.GetProperty(p.Name);
                                if (prop != null)
                                {
                                    prop.SetValue(instance, Convert.ChangeType(value, prop.PropertyType));
                                }
                            }
                        }
                        return instance;
                    }
                }
            });
    }

    private async Task<string?> ExecuteWrapper<TArgs>(TArgs typedArgs, CancellationToken ct) where TArgs : class
    {
        var pluginType = _assemblies
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.GetCustomAttribute<ToolTypeAttribute>()?.Name.Equals(_toolDef.Type, StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new TypeLoadException($"No plugin type found for tool type '{_toolDef.Type}'");

        var method = pluginType.GetMethod("Run", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?? throw new MissingMethodException(pluginType.Name, "Run");

        var instance = _sp.GetRequiredService(pluginType);

        var flatArgs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var aiArgs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in _args ?? new AIFunctionArguments())
        {
            if (arg.Value is JsonElement json && json.ValueKind == JsonValueKind.Object)
            {
                var parsed = ConvertArgsToDictionary(json);
                foreach (var kvp in parsed)
                    aiArgs[kvp.Key] = kvp.Value;
            }
            else
            {
                aiArgs[arg.Key] = arg.Value;
            }
        }

        foreach (var param in _toolDef.Parameters)
        {
            flatArgs[param.Name] = aiArgs.TryGetValue(param.Name, out var v) ? v : null;
        }

        flatArgs["threadId"] = _threadId;
        flatArgs["toolName"] = _toolDef.Name;

        var transformer = instance as IToolArgumentTransformer ?? new DeclarativeArgumentTransformer();
        var invokeArgs = transformer.TransformArguments(method, flatArgs, _toolDef);

        if (instance is IYamlToolAware aware)
        {
            aware.SetToolDefinition(_toolDef);
        }
        var result = method.Invoke(instance, invokeArgs);

        if (result is Task<string> taskStr) return await taskStr;
        if (result is Task task) { await task; return null; }

        return result?.ToString();
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
