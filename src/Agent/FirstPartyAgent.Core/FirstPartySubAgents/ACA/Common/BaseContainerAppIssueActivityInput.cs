using System.ComponentModel;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
public record BaseContainerAppIssueActivityInput
{
    [Description("Azure subscription id")]
    public string SubscriptionId { get; init; } = string.Empty;
    [Description("Azure Resource Group name")]
    public string ResourceGroupName { get; init; } = string.Empty;

    [Description("Managed Environment Name")]
    public string? ManagedEnvironmentName { get; init; }

    [Description("The start of the time range to analyze.")]
    public DateTime FromDate { get; init; }

    [Description("The end of the time range to analyze. Always ensure that it should be greater than 'FromDate'")]
    public DateTime ToDate { get; init; }

    [Description("The Azure region where the container app is deployed. Example: 'francecentral'")]
    public string Region { get; init; } = string.Empty;


    [Description("The Incident ID (IcM ID) associated with the issue. Example: '622811149'")]
    public string? IcmId { get; init; } = string.Empty;

    [Description("Summary of issue that is being investigated")]
    public string IssueDescription { get; init; } = string.Empty;
}
