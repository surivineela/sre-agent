using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.ContainerAppsRemediation;

public sealed record ContainerAppsRemediationAgentInput(
    string Input,
    IReadOnlyList<string> ToolSignatures,
    ThreadContext Context);
