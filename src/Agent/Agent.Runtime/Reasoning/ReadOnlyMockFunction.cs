// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Core.Attributes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// A mock AIFunction that simulates write operations in read-only mode
/// </summary>
public class ReadOnlyMockFunction : AIFunction
{
    private readonly MethodInfo _originalMethod;
    private readonly object _instance;
    private readonly ILogger? _logger;
    private readonly string _customMessage;

    public ReadOnlyMockFunction(MethodInfo originalMethod, object instance, string functionName, ILogger? logger, string? customMessage = null)
    {
        _originalMethod = originalMethod;
        _instance = instance;
        _logger = logger;
        _customMessage = customMessage ?? $"[Read-Only Mode] Simulated execution of {functionName}. Operation would complete successfully.";

        // Create a temporary function to get the proper metadata
        var tempFunction = AIFunctionFactory.Create(_originalMethod, _instance, name: functionName);

        // Copy metadata from the temporary function
        Name = tempFunction.Name;
        Description = tempFunction.Description;
    }

    public override string Name { get; }
    public override string Description { get; }

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var argString = JsonSerializer.Serialize(arguments, new JsonSerializerOptions { WriteIndented = false });

        _logger?.LogInformation("Read-only mode: Mocking call to {MethodName} with arguments: {Arguments}",
            _originalMethod.Name, argString);

        // Return appropriate type based on method signature
        var returnType = _originalMethod.ReturnType;

        if (returnType == typeof(Task<string>))
        {
            return ValueTask.FromResult<object?>(Task.FromResult(_customMessage));
        }
        else if (returnType == typeof(string))
        {
            return ValueTask.FromResult<object?>(_customMessage);
        }
        else if (returnType == typeof(Task))
        {
            return ValueTask.FromResult<object?>(Task.CompletedTask);
        }
        else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var taskResultType = returnType.GetGenericArguments()[0];
            if (taskResultType == typeof(string))
            {
                return ValueTask.FromResult<object?>(Task.FromResult(_customMessage));
            }
        }

        // For other return types, return default value
        var defaultValue = returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        return ValueTask.FromResult(defaultValue);
    }
}
