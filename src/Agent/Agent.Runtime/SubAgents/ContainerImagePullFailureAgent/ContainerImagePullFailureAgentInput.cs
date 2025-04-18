using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;

public sealed record ContainerImagePullFailureAgentInput(
    string ResourceId,
    IReadOnlyList<string> ToolSignatures,
    Guid ThreadId);
