// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Framework.Reasoning.Models;

namespace Agent.Web.Models.ExtendedAgents;

public class GenericResourceModel
{

    [JsonPropertyName( "api_version")]
    public required string ApiVersion { get; set; }

    [JsonPropertyName( "kind")]
    public required string Kind { get; set; }

    [JsonPropertyName( "metadata")]
    public required YamlMetadata Metadata { get; set; }

    [JsonPropertyName("spec")]
    public JsonElement Spec { get; set; }
}
