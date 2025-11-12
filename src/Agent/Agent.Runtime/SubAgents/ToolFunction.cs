// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Runtime.SubAgents;

public abstract class IToolFunction
{
    public abstract AIFunction ToolFunction { get; }
}

public interface IToolFunction202
{
    AIFunction ExecuteFunction { get; }
}

public sealed class ToolFunction200 : IToolFunction
{
    private readonly AIFunction _execFunction;

    public override AIFunction ToolFunction => _execFunction;

    public ToolFunction200(
        Delegate executeFunction)
    {
        _execFunction = AIFunctionFactory.Create(executeFunction);
    }

    public ToolFunction200(
        AIFunction func)
    {
        _execFunction = func;
    }
}

public sealed class DeferredToolFunction200<T> : IToolFunction where T : notnull
{
    private readonly IServiceProvider _sp;
    private readonly MethodInfo _methodInfo;

    public DeferredToolFunction200(IServiceProvider sp, MethodInfo methodInfo)
    {
        _sp = sp;
        _methodInfo = methodInfo;
    }

    public override AIFunction ToolFunction
    {
        get
        {
            var instance = _sp.GetRequiredService<T>();
            return AIFunctionFactory.Create(_methodInfo, instance);
        }
    }
}

public sealed class ToolFunction202 : IToolFunction, IToolFunction202
{
    private readonly AIFunction _submitFunction;
    private readonly AIFunction _execFunction;

    public override AIFunction ToolFunction => _submitFunction;

    public AIFunction ExecuteFunction => _execFunction;

    public ToolFunction202(
        Delegate submitFunction,
        Delegate executeFunction)
    {
        _submitFunction = AIFunctionFactory.Create(submitFunction);
        _execFunction = AIFunctionFactory.Create(executeFunction);
    }
}

public sealed class DeferredToolFunction202<T> : IToolFunction, IToolFunction202 where T : notnull
{
    private readonly IServiceProvider _sp;
    private readonly MethodInfo _submitMethodInfo;
    private readonly MethodInfo _executeMethodInfo;

    public DeferredToolFunction202(IServiceProvider sp, MethodInfo submitMethodInfo, MethodInfo executeMethodInfo)
    {
        _sp = sp;
        _submitMethodInfo = submitMethodInfo;
        _executeMethodInfo = executeMethodInfo;
    }

    public override AIFunction ToolFunction
    {
        get
        {
            var instance = _sp.GetRequiredService<T>();
            return AIFunctionFactory.Create(_submitMethodInfo, instance);
        }
    }

    public AIFunction ExecuteFunction
    {
        get
        {
            var instance = _sp.GetRequiredService<T>();
            return AIFunctionFactory.Create(_executeMethodInfo, instance);
        }
    }
}


