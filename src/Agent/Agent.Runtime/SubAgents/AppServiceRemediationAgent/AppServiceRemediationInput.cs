namespace Agent.Runtime.SubAgents.AppServiceRemediation;

public sealed record AppServiceRemediationInput(
    List<string> AppServiceResourceIds);
