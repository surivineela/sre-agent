using System.ComponentModel.DataAnnotations;

namespace Agent.Runtime.Models.ExtendedAgents;

public class ExtendedAgentError
{
    [Required]
    public string Status { get; set; } = "error";

    [Required]
    public string ErrorCode { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ErrorDetails? Details { get; set; }
}

public class ErrorDetails
{
    public List<ErrorField>? Errors { get; set; }
}

public class ErrorField
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
