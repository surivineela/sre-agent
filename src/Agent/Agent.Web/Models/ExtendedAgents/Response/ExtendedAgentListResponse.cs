// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Web.Models.ExtendedAgents.Response;

public record ExtendedAgentsListResponse(
    [property: JsonPropertyName("data")] PaginatedList<ExtendedAgentApiModel> Data,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp
)
{
    public static ExtendedAgentsListResponse FromRuntime(PaginatedList<YamlAgentDescriptor> runtimeModel)
    {
        return new ExtendedAgentsListResponse(
            Data: new PaginatedList<ExtendedAgentApiModel>(
                 runtimeModel.Select(agent => ExtendedAgentApiModel.FromRuntime(agent)).ToList(),
                runtimeModel.TotalCount,
                0,
                 runtimeModel.TotalCount
            ),
            Timestamp: DateTime.UtcNow
        );
    }
}

public record ExtendedAgentsListData(
    [property: JsonPropertyName("agents")] List<ExtendedAgentApiModel> Agents
)
{
    public static ExtendedAgentsListData FromRuntime(PaginatedList<YamlAgentDescriptor> runtimeModel)
    {
        return new ExtendedAgentsListData(
            Agents:  new PaginatedList<ExtendedAgentApiModel>(
                 runtimeModel.Select(agent => ExtendedAgentApiModel.FromRuntime(agent)).ToList(),
                runtimeModel.TotalCount,
                0,
                 runtimeModel.TotalCount
            )
        );
    }
}
