using System;
using System.Collections.Generic;
using System.ComponentModel;
using Agent.Core.Models;

namespace Agent.Runtime.SubAgents.FunctionAppExecutionFailuresAgent;
public sealed record FunctionAppExecutionFailuresAgentInput(
    [Description("Full azure resource id of the Azure Function app resource needs to be investigated. Should restart with /subscriptions/...")] string FunctionAppResourceId,
    [Description("Signature of a list of tools available for the agent to use")] IReadOnlyList<string> ToolSignatures,
    [Description("Thread id")] Guid ThreadId);
