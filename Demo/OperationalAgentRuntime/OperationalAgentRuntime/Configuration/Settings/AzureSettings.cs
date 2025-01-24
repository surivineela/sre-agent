using System.ComponentModel.DataAnnotations;

namespace OperationalAgentRuntime.Configuration.Settings;

public class AzureSettings
{
    [Required]
    public OpenAISettings OpenAI { get; set; } = new();
}

public class OpenAISettings
{
    [Required]
    public string DeploymentName { get; set; } = string.Empty;

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;
} 