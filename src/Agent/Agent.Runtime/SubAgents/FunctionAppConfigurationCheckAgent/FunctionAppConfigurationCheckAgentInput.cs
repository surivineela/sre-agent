using System;
using System.Collections.Generic;

namespace Agent.Runtime.SubAgents.FunctionAppConfigurationCheck;

/// <summary>
/// Input for the Function App Configuration Check Agent
/// </summary>
public record FunctionAppConfigurationCheckAgentInput(
    string FunctionAppResourceId,
    IReadOnlyList<string> ToolSignatures,
    Guid ThreadId);
