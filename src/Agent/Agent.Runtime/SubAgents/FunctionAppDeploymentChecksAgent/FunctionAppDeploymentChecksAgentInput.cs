using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.FunctionAppDeploymentChecksAgent;

/// <summary>
/// Input for the Function App Deployment Checks Agent
/// </summary>
public record FunctionAppDeploymentChecksAgentInput(
    [Description("Full azure resource id of the Azure Function app resource to be investigated. Should start with /subscriptions/...")] string FunctionAppResourceId,
    [Description("Signature of a list of tools available for the agent to use")] IReadOnlyList<string> ToolSignatures,
    [Description("Thread id")] Guid ThreadId);
