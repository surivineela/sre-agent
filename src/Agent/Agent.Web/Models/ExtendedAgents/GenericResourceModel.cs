// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Web.Models.ExtendedAgents;

public class GenericResourceModel
{

    [JsonPropertyName("api_version")]
    public string? ApiVersion { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("metadata")]
    public YamlMetadata? Metadata { get; set; }

    [JsonPropertyName("spec")]
    public JsonElement Spec { get; set; }
}
