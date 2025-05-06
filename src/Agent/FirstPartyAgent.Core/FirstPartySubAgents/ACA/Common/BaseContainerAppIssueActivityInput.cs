using System.ComponentModel;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
public record BaseContainerAppIssueActivityInput
{
    [Description("Subscription id")]
    public string SubscriptionId { get; init; }
    [Description("Resource Group name")]
    public string ResourceGroupName { get; init; }

    [Description("The start of the time range to analyze.")]
    public DateTime FromDate { get; init; }

    [Description("The end of the time range to analyze.")]
    public DateTime ToDate { get; init; }

    [Description("The Azure region where the container app is deployed. Example: 'francecentral'")]
    public string? Region { get; init; } = string.Empty;


    [Description("The Incident ID (IcM ID) associated with the issue. Example: '622811149'")]
    public string? IcmId { get; init; } = string.Empty;

    [Description("Summary of issue that is being investigated")]
    public string IssueDescription { get; init; } = string.Empty;
}
