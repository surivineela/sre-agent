namespace Agent.Runtime.SubAgents.AppServiceRemediation;

public sealed record AppServiceRemediationAgentInput(
    AppServiceRemediationInput Input,
    IReadOnlyList<string> ToolSignatures);
