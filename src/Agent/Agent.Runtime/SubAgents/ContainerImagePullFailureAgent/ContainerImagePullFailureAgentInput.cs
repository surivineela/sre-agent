using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;

public sealed record ContainerImagePullFailureAgentInput(
    string Input,
    string ResourceId,
    string ImageReference,
    string ErrorMessage,
    IReadOnlyList<string> ToolSignatures,
    ThreadContext Context);
