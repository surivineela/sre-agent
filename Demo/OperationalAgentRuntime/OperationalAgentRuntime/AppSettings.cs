using System.ComponentModel.DataAnnotations;

namespace OperationalAgentRuntime;

public class AppSettings
{
    // Add any general application settings here
}

public class AzureSettings
{
    [Required]
    public OpenAISettings OpenAI { get; set; } = new();

    [Required]
    public string TeamsEndpoint { get; set; } = string.Empty;
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
