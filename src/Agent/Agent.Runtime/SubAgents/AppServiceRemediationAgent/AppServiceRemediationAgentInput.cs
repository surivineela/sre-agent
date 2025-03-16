namespace Agent.Runtime.SubAgents.AppServiceRemediation;

public sealed record AppServiceRemediationAgentInput(
    string Input,
    IReadOnlyList<string> ToolSignatures,
    string ThreadId);
