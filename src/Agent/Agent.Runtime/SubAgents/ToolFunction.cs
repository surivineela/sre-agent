using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.SubAgents;

public abstract class IToolFunction
{
    public abstract AIFunction ToolFunction { get; }
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
}

public sealed class ToolFunction202 : IToolFunction
{
    private readonly AIFunction _submitFunction;
    private readonly AIFunction _execFunction;

    public override AIFunction ToolFunction => _submitFunction;

    public AIFunction ExecueFunction => _execFunction;

    public ToolFunction202(
        Delegate submitFunction,
        Delegate executeFunction)
    {
        _submitFunction = AIFunctionFactory.Create(submitFunction);
        _execFunction = AIFunctionFactory.Create(executeFunction);
    }
}

