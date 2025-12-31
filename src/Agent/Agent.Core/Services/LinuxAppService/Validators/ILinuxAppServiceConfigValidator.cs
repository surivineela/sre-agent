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
    /// <returns>A list of <see cref="LinuxAppServiceConfigIssue"/> if configuration issues are detected; otherwise an empty list.</returns>
    Task<List<LinuxAppServiceConfigIssue>> ValidateAsync(LinuxAppServiceConfiguration siteConfig);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinuxAppServiceConfigIssueType
{
    InvalidLinuxFxVersion,
    InvalidAppSettingValue
}
