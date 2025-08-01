using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Agent.Plugins.Models;

public class GenevaActionsConfigCosmos
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public List<GenevaActionConfig>? GenevaActions { get; set; }
    public int TeamId { get; set; }
}

public class GenevaActionConfig : GenevaActionConfigBase
{
    [Required]
    public bool IsWriteAction { get; set; }
    [Required]
    public bool IsAllowedOnExternalSubs { get; set; }
    public bool IsApprovalNeeded { get; set; }
    public Guid? ServiceTreeId { get; set; }

    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public LogLevel? IcmLogLevel { get; set; } = LogLevel.None;
}

public class GenevaActionConfigBase
{
    public string ActionName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public List<string> WorkflowInputParameters { get; set; } = new List<string>();
}
