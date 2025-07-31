using System.ComponentModel.DataAnnotations;
using Agent.Core.Models.Api.v1;
using Agent.Framework;

namespace Agent.Runtime.Models.ExtendedAgents;

public class AgentToolsList
{
    [Required]
    public AgentToolsListData Data { get; set; } = new();

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AgentToolsListData
{
    public List<YamlToolDefinitionBase> Tools { get; set; } = new();
}
