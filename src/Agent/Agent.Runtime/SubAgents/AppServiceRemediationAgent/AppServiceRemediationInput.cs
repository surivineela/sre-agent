using System.ComponentModel;

namespace Agent.Runtime.SubAgents.AppServiceRemediation;

public sealed record AppServiceRemediationInput(
    [Description("Detailed description of the issue. Must include azure resource id of the webapp or function app resource.")]
    string message,
    [Description("List of resources that need remediation.")]
    List<string> AppServiceResourceIds);
