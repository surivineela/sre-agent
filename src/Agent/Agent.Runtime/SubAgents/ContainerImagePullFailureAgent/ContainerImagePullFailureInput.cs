using System.Collections.Generic;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;

public sealed record ContainerImagePullFailureInput(
    [Description("Resource ID of the affected Container App.")]
    string resourceId);
