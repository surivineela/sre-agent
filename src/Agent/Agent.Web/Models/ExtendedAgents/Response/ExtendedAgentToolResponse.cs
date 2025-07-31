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
    [property: JsonPropertyName("tools")] PaginatedList<ExtendedAgentToolApiModel> Tools
)
{
    public static ExtendedAgentToolsData FromRuntime(PaginatedList<YamlToolDefinitionBase> runtimeModel)
    {
        return new ExtendedAgentToolsData(
            Tools: new PaginatedList<ExtendedAgentToolApiModel>(runtimeModel.Select(ApiToRuntimeMapper.ToApiTool),
                runtimeModel.Count,
        0,
                runtimeModel.Count

            )
        );
    }
}
