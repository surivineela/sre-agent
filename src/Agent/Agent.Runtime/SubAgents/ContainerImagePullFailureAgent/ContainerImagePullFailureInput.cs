using System.Collections.Generic;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;

public sealed record ContainerImagePullFailureInput(
    [Description("Detailed description of the image pull failure issue with full azure resource id of the Azure Container Apps resource.")]
    string message,
    [Description("Resource ID of the affected Container App.")]
    string resourceId,
    [Description("Docker image reference that failed to pull.")]
    string imageReference,
    [Description("Error message from the container logs related to the image pull failure.")]
    string errorMessage);
