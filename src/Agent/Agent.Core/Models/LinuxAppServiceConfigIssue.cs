using Agent.Core.Services.LinuxAppService.Validators;

namespace Agent.Core.Models;

public sealed record LinuxAppServiceConfigIssue(
  string ResourceId,
  string SiteName,
  string Location,
  LinuxAppServiceConfigIssueType Type,
  string Details,
  string Recommendation);

