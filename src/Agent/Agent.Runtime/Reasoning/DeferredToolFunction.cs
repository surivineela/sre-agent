// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Attributes;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Creates a tool from reflection (a MethodInfo)
/// </summary>
public sealed class DeferredToolFunction<TContext> : IDeferredToolFunction<TContext> where TContext : class
{
    // Common fields
    private readonly IServiceProvider _sp;

    private readonly MethodInfo _methodInfo;
    private readonly Type _pluginType;
    private readonly string _reflectionBasedName;

    /// <summary>
    /// Private constructor to enforce creation via static factory methods.
    /// </summary>
    public DeferredToolFunction(IServiceProvider sp, Type pluginType, MethodInfo methodInfo, string name)
    {
        _sp = sp;
        _pluginType = pluginType;
        _methodInfo = methodInfo;
        _reflectionBasedName = name;
    }

    public string GetPluginCategory()
    {
        var attribute = _pluginType.GetCustomAttribute<AgentToolPluginAttribute>();
        if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Category))
        {
            return attribute.Category;
        }
        return string.Empty;
    }

    public string GetPluginResourceType()
    {
        var attribute = _pluginType.GetCustomAttribute<AgentToolPluginAttribute>();
        if (attribute != null && !string.IsNullOrWhiteSpace(attribute.ResourceType))
        {
            return attribute.ResourceType;
        }
        return string.Empty;
    }

    public string GetPluginName()
    {
        return _pluginType.Name;
    }

    public MethodInfo? MethodInfo => _methodInfo;

    /// <summary>
    /// Creates the AIFunction based on the source information (Reflection or YAML).
    /// </summary>
    public AIFunction GetToolFunction(Guid? threadId = null, Agent<TContext>? agent = null)
    {
        return GetToolFunction(threadId, null, agent);
    }

    /// <summary>
    /// Creates the AIFunction based on the source information (Reflection or YAML) with agent mode support.
    /// </summary>
    public AIFunction GetToolFunction(Guid? threadId, string? agentMode, Agent<TContext>? agent = null)
    {
        // Case 1: The tool was defined by reflecting over source code.
        if (_methodInfo is not null)
        {
            return CreateFromReflection(threadId, agentMode);
        }

        throw new InvalidOperationException("DeferredToolFunction was not properly initialized. Both reflection and YAML sources are null.");
    }

    private AIFunction CreateFromReflection(Guid? threadId, string? agentMode)
    {
        var instance = _sp.GetRequiredService(_pluginType!);

        // Check if this is a write operation in read-only mode
        var writeActionAttr = _methodInfo!.GetCustomAttribute<WriteActionAttribute>();
        var isReadOnlyMode = IsReadOnlyMode(agentMode);

        if (writeActionAttr != null && isReadOnlyMode && !writeActionAttr.RunInReadOnlyMode)
        {
            // Create a wrapper that returns mock responses instead of executing
            return CreateReadOnlyMockFunction(instance, threadId, writeActionAttr.ReadOnlyMessage);
        }

        // Standard tool creation logic (existing code continues here...)
        if (threadId is not null)
        {
            // Check for public ThreadId property first
            var threadIdPropertyPublic = _pluginType!.GetProperty("ThreadId", BindingFlags.Instance | BindingFlags.Public);
            if (threadIdPropertyPublic is not null && threadIdPropertyPublic.PropertyType == typeof(Guid?))
            {
                threadIdPropertyPublic.SetValue(instance, threadId);
            }

            // Check all private fields and if they have ThreadId, set it on those objects
            var privateFields = _pluginType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in privateFields)
            {
                var fieldValue = field.GetValue(instance);
                if (fieldValue != null)
                {
                    var fieldType = fieldValue.GetType();

                    var threadIdProperty = fieldType.GetProperty("ThreadId", BindingFlags.Instance | BindingFlags.Public);
                    if (threadIdProperty != null && threadIdProperty.PropertyType == typeof(Guid?) && threadIdProperty.CanWrite)
                    {
                        threadIdProperty.SetValue(fieldValue, threadId);
                    }

                    var threadIdField = fieldType.GetField("ThreadId", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (threadIdField != null && threadIdField.FieldType == typeof(Guid?))
                    {
                        threadIdField.SetValue(fieldValue, threadId);
                    }
                }
            }
        }

        if (instance is ContextToolTarget<TContext> contextToolTarget)
        {
            return ContextAIFunction<TContext>.Create(_methodInfo!, contextToolTarget, name: _reflectionBasedName!);
        }

        // Generic-agnostic reflective wrapping for any ContextToolTarget<*>.
        if (TryCreateContextWrapperForArbitraryContext(instance, out var wrapped))
        {
            return wrapped;
        }

        // Check if the method should keep original return type
        if (_methodInfo!.ShouldKeepOriginalReturnType())
        {
            var options = new AIFunctionFactoryOptions
            {
                Name = _reflectionBasedName!,
                MarshalResult = (result, type, cancellationToken) => new ValueTask<object?>(result)
            };
            return AIFunctionFactory.Create(_methodInfo!, instance, options);
        }

        return AIFunctionFactory.Create(_methodInfo!, instance, name: _reflectionBasedName!);
    }

    private bool TryCreateContextWrapperForArbitraryContext(object instance, out AIFunction wrapped)
    {
        wrapped = null!;
        var type = instance.GetType();
        var baseType = type.BaseType;

        if (baseType is null || !baseType.IsGenericType) return false;
        if (baseType.GetGenericTypeDefinition() != typeof(ContextToolTarget<>)) return false;

        var actualContextType = baseType.GetGenericArguments()[0];

        // Build ContextAIFunction<actualContextType>.Create(MethodInfo, object, string?, string?)
        var wrapperGenericType = typeof(ContextAIFunction<>).MakeGenericType(actualContextType);
        var createMethod = wrapperGenericType.GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static,
            new[] { typeof(MethodInfo), typeof(object), typeof(string), typeof(string) });

        if (createMethod == null)
        {
            return false;
        }

        wrapped = (AIFunction)createMethod.Invoke(null, [_methodInfo!, instance, _reflectionBasedName!, null])!;
        return true;
    }

    private static bool IsReadOnlyMode(string? agentMode)
    {
        return string.Equals(agentMode, ActionMode.ReadOnly.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private ReadOnlyMockFunction CreateReadOnlyMockFunction(object instance, Guid? threadId, string? customMessage)
    {
        var logger = _sp.GetService<ILogger<DeferredToolFunction<TContext>>>();
        return new ReadOnlyMockFunction(_methodInfo!, instance, _reflectionBasedName!, logger, customMessage);
    }
}
