// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Agent.Core.Services.LinuxAppService.Validators;

/// <summary>
/// Defines how to compare the actual value with the expected value.
/// </summary>
public enum ExpectedValueOperator
{
    /// <summary>
    /// Exact equality comparison (==).
    /// </summary>
    Equals,

    /// <summary>
    /// Contains check for substring match.
    /// </summary>
    Contains
}

/// <summary>
/// Represents a configuration rule for validating app settings.
/// </summary>
public record AppSettingValidationRule(
    string SettingName,
    string[] AllowedValues,
    ExpectedValueOperator ExpectedValueOperator,
    string RecommendationMessage);

/// <summary>
/// Validates app settings configuration for Linux App Services.
/// </summary>
public class AppSettingValidator : ILinuxAppServiceConfigValidator
{
    private readonly ArmHelper _armHelper;
    private readonly ILogger<AppSettingValidator> _logger;

    private static readonly List<AppSettingValidationRule> ValidationRules =
    [
        new AppSettingValidationRule(
            SettingName: "ApplicationInsightsAgent_EXTENSION_VERSION",
            AllowedValues: ["~3", "disabled"],
            ExpectedValueOperator: ExpectedValueOperator.Equals,
            RecommendationMessage: "To be able to use Application Insights for Linux AppServices," +
                                   " update ApplicationInsightsAgent_EXTENSION_VERSION AppSetting to ~3")
    ];

    public LinuxAppServiceConfigIssueType IssueType => LinuxAppServiceConfigIssueType.InvalidAppSettingValue;

    public AppSettingValidator(
        ArmHelper armHelper,
        ILogger<AppSettingValidator> logger)
    {
        _armHelper = armHelper;
        _logger = logger;
    }

    /// <summary>
    /// Validates the app settings configuration against defined rules.
    /// </summary>
    /// <param name="siteConfig">The Linux App Service configuration to validate</param>
    /// <returns>A list of LinuxAppServiceConfigIssue for each validation failure; empty list if all validations pass.</returns>
    public async Task<List<LinuxAppServiceConfigIssue>> ValidateAsync(LinuxAppServiceConfiguration siteConfig)
    {
        string appSettingsJson;
        try
        {
            // Fetch app settings from ARM
            appSettingsJson = await _armHelper.GetAppSettings(siteConfig.ResourceId);
        }
        catch (Exception ex)
        {
            // Log warning and skip validation for this resource
            _logger.LogInternalWarning(ex, "Failed to fetch app settings for {ResourceId}", siteConfig.ResourceId);
            return [];
        }

        JObject jsonObject;
        try
        {
            jsonObject = JObject.Parse(appSettingsJson);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to parse app settings JSON for {ResourceId}", siteConfig.ResourceId);
            return [];
        }

        // Validate each rule and collect all issues
        var issues = new List<LinuxAppServiceConfigIssue>();
        foreach (var rule in ValidationRules)
        {
            var actualValue = jsonObject["properties"]?[rule.SettingName]?.ToString();

            // Skip if setting is missing or empty
            if (string.IsNullOrEmpty(actualValue))
            {
                continue;
            }

            // Check if the value matches the expected value based on operator
            bool isValid = rule.ExpectedValueOperator switch
            {
                ExpectedValueOperator.Equals => rule.AllowedValues.Contains(actualValue),
                ExpectedValueOperator.Contains => rule.AllowedValues.Any(allowed => actualValue.Contains(allowed, StringComparison.Ordinal)),
                _ => false
            };

            if (!isValid)
            {
                var allowedValuesStr = string.Join(" or ", rule.AllowedValues.Select(v => $"'{v}'"));
                issues.Add(new LinuxAppServiceConfigIssue(
                    ResourceId: siteConfig.ResourceId,
                    SiteName: siteConfig.Name,
                    Location: siteConfig.Location,
                    Type: IssueType,
                    Details: $"{rule.SettingName} is set to '{actualValue}' but should be {allowedValuesStr}",
                    Recommendation: rule.RecommendationMessage
                ));
            }
        }

        return issues;
    }
}
