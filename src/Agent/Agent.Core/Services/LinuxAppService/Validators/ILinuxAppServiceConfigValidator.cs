using System.Text.Json.Serialization;
using Agent.Core.Models;

namespace Agent.Core.Services.LinuxAppService.Validators;

public interface ILinuxAppServiceConfigValidator
{
    /// <summary>
    /// The type of configuration issue detected.
    /// </summary>
    LinuxAppServiceConfigIssueType IssueType { get; }

    /// <summary>
    /// Validates and Identifies config issues.
    /// </summary>
    /// <param name="siteConfig">The Linux App Service configuration to validate.</param>
    /// <returns>A <see cref="LinuxAppServiceConfigIssue"/> if a configuration issue is detected; otherwise null.</returns>
    Task<LinuxAppServiceConfigIssue?> ValidateAsync(LinuxAppServiceConfiguration siteConfig);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinuxAppServiceConfigIssueType
{
    InvalidLinuxFxVersion
}
