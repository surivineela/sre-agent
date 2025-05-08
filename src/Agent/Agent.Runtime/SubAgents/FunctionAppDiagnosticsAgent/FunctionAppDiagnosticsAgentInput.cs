using System;
using System.Collections.Generic;
using System.ComponentModel;
using Agent.Core.Models;

namespace Agent.Runtime.SubAgents.FunctionAppDiagnosticsAgent;
public sealed record FunctionAppDiagnosticsAgentInput(
    [Description("Full azure resource id of the Azure Function app resource needs to be investigated. Should restart with /subscriptions/...")] string FunctionAppResourceId,
    [Description("Dictionary of tool signatures available for different agents")] IReadOnlyDictionary<string, IReadOnlyList<string>> ToolSignatures,
    [Description("Thread id")] Guid ThreadId);
