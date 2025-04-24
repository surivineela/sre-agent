// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using System.Reflection;

namespace Agent.Runtime.Helpers;

public static class ToolHelper
{
    public static IList<string> AddPlugin<T>(this IList<string> signatures)
    {
        var pluginType = typeof(T);

        // Get all public methods with Description attribute
        var methodsToRegister = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() != null);

        foreach (var method in methodsToRegister)
        {
            var signature = GetSignature(method);
            signatures.Add(signature);
        }

        return signatures;
    }

    public static IList<string> AddTool<T>(this IList<string> signatures, Expression<Func<T, Delegate>> executeFunctionSelector)
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
        var signature = GetSignature(methodInfo);
        signatures.Add(signature);

        return signatures;
    }

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

    public static string GetSignature(
        Expression<Func<Delegate>> actionSelector)
    {
        var actionMethod = GetMethod(actionSelector);
        var sig = GetSignature(actionMethod);
        return sig;
    }

    public static string GetSignature(MethodInfo method)
    {
        if (method.DeclaringType is null
            || method.DeclaringType.FullName is null)
        {
            throw new ArgumentNullException("method's DeclaringType.FullName not exist");
        }

        var className = method.DeclaringType.FullName;
        var methodName = method.Name;
        var parameters = string.Join(", ", method.GetParameters()
            .Select(p => p.ParameterType.FullName));

        return $"{className}.{methodName}({parameters})";
    }

    private static MethodInfo GetMethod<R>(Expression<Func<R>> selector)
        where R : Delegate
    {
        if (selector.Body is UnaryExpression unaryExpression
            && unaryExpression.Operand is MethodCallExpression methodCallExpression
            && methodCallExpression.Object is ConstantExpression constantExpression
            && constantExpression.Value is MethodInfo methodInfo)
        {
            return methodInfo;
        }

        throw new ArgumentOutOfRangeException(nameof(selector));
    }
}
