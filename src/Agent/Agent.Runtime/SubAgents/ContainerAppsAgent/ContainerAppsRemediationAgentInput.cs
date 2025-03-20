namespace Agent.Runtime.SubAgents.ContainerAppsRemediation;

public sealed record ContainerAppsRemediationAgentInput(
    string Input,
    IReadOnlyList<string> ToolSignatures,
    string ThreadId);
