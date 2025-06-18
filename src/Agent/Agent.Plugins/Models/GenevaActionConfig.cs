using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Agent.Plugins.Models;

public class GenevaActionsConfigCosmos
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public List<GenevaActionConfig> GenevaActions { get; set; }
    public int TeamId { get; set; }
}

public class GenevaActionConfig : GenevaActionConfigBase
{
    [Required]
    public bool IsWriteAction { get; set; }
    [Required]
    public bool IsAllowedOnExternalSubs { get; set; }
}

public class GenevaActionConfigBase
{
    public string ActionName { get; set; }
    public string TenantId { get; set; }
    public string WorkflowName { get; set; }
    public List<string> WorkflowInputParameters { get; set; }
}