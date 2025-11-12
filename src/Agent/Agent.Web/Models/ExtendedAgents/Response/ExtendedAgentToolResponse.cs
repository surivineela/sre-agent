// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;
using Agent.Web.Services;

namespace Agent.Web.Models.ExtendedAgents.Response;

public record ExtendedAgentToolsResponse(
    [property: JsonPropertyName("data")] ExtendedAgentToolsData Data
)
{
    public static ExtendedAgentToolsResponse FromRuntime(PaginatedList<YamlToolDefinitionBase> runtimeModel)
    {
        return new ExtendedAgentToolsResponse(
            Data: ExtendedAgentToolsData.FromRuntime(runtimeModel)

        );
    }
}

public record ExtendedAgentToolsData(
    [property: JsonPropertyName("tools")] PaginatedResponse<ExtendedAgentToolApiModel> Tools
)
{
    public static ExtendedAgentToolsData FromRuntime(PaginatedList<YamlToolDefinitionBase> runtimeModel)
    {
        var convertedTools = runtimeModel.Select(ApiToRuntimeMapper.ToApiTool).ToList();
        var paginatedTools = new PaginatedList<ExtendedAgentToolApiModel>(convertedTools, runtimeModel.TotalCount, runtimeModel.PageIndex, runtimeModel.PageSize);

        return new ExtendedAgentToolsData(
            Tools: PaginatedResponse<ExtendedAgentToolApiModel>.FromPaginatedList(paginatedTools)
        );
    }
}
