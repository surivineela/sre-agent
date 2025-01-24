using System.ComponentModel.DataAnnotations;

namespace OperationalAgentRuntime.Configuration.Settings;

public class AppSettings
{
    [Required]
    public string ApplicationName { get; set; } = string.Empty;
    
    [Required]
    public string Environment { get; set; } = string.Empty;
} 