// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Linq.Expressions;
using System.Text;
using Agent.Runtime.SubAgents;

namespace Agent.Runtime;

public class AgentToolsRegistry
{
    private readonly IToolsRepository _toolsRepository;
    private readonly List<string> _toolSignatures = new();

    public AgentToolsRegistry(IToolsRepository toolsRepository)
    {
        _toolsRepository = toolsRepository;
    }

    public IReadOnlyList<string> ToolSignatures => _toolSignatures.AsReadOnly();

    /// <summary>
    /// Extracts method information from an expression that selects a method
    /// </summary>
    /// <typeparam name="T">The plugin definition type</typeparam>
    /// <param name="executeFunctionSelector">Expression that selects the method</param>
    /// <returns>The MethodInfo for the selected method</returns>
    public static MethodInfo GetMethodFromExpression<T>(Expression<Func<T, Delegate>> executeFunctionSelector)
    {
        // Check if the body is a UnaryExpression with Convert node type
        if (executeFunctionSelector.Body is UnaryExpression unaryExpr &&
            unaryExpr.NodeType == ExpressionType.Convert)
        {
            // Check if the operand is a MethodCallExpression 
            if (unaryExpr.Operand is MethodCallExpression methodCallExpr &&
                methodCallExpr.Method.Name == "CreateDelegate")
            {
                // The first argument of CreateDelegate should be the actual method info we want
                if (methodCallExpr.Object is ConstantExpression objExpr &&
                    objExpr.Value is MethodInfo targetMethodInfo)
                {
                    return targetMethodInfo;
                }
            }
        }

        throw new ArgumentException("Could not extract method info from the expression. Make sure the method exists.",
            nameof(executeFunctionSelector));
    }

    /// <summary>
    /// Registers a specific method from a plugin class as a tool
    /// </summary>
    /// <typeparam name="T">The plugin definition type</typeparam>
    /// <param name="executeFunctionSelector">Expression that selects the method to register</param>
    public void RegisterTool<T>(Expression<Func<T, Delegate>> executeFunctionSelector)
    {
        // Extract the method info from the expression
        var methodInfo = GetMethodFromExpression(executeFunctionSelector);

        // Verify the method has the Description attribute
        if (methodInfo.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() == null)
        {
            throw new ArgumentException($"Method {methodInfo.Name} on type {methodInfo.DeclaringType?.FullName} " +
                $"does not have a Description attribute");
        }

        // Generate the signature and add it to the list
        var signature = _toolsRepository.GetSignature(methodInfo);
        _toolSignatures.Add(signature);
    }


    /// <summary>
    /// Registers all methods with Description attribute from a plugin class
    /// </summary>
    /// <typeparam name="T">The plugin definition type</typeparam>
    public void RegisterPlugin<T>()
    {
        var pluginType = typeof(T);

        // Get all public methods with Description attribute
        var methodsToRegister = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() != null);

        foreach (var method in methodsToRegister)
        {
            var signature = _toolsRepository.GetSignature(method);
            _toolSignatures.Add(signature);
        }
    }
}
