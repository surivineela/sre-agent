// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public record HypothesisDetailsDocument(
    string Id,
    string AgentTaskId,
    string Title,
    string Description,
    IEnumerable<HypothesisStep> Steps,
    IEnumerable<HypothesisTreeItem> Children,
    HypothesisStatus Status
) : ICosmosDocument
{
    public string DocumentType => "HypothesisDetails";
    public string PartitionKey => AgentTaskId;
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public static HypothesisDetailsDocument FromDomainModel(HypothesisDetails hypothesisDetails) =>
        new(
            Id: hypothesisDetails.Id.ToString(),
            AgentTaskId: hypothesisDetails.AgentTaskId.ToString(),
            Title: hypothesisDetails.Title,
            Description: hypothesisDetails.Description,
            Steps: hypothesisDetails.Steps,
            Children: hypothesisDetails.Children,
            Status: hypothesisDetails.Status
        );

    public HypothesisDetails ToDomainModel() =>
        new()
        {
            Id = Guid.Parse(Id),
            Title = Title,
            Description = Description,
            Steps = Steps,
            Children = Children,
            Status = Status,
            AgentTaskId = Guid.Parse(AgentTaskId),
            ParentHypothesisDescription = string.Empty,
        };
}
