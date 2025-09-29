using Agent.Core.Configuration;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Runtime.Services;

/// <summary>
/// Service responsible for dynamically loading incident management agents based on configuration.
/// This replaces the need for separate platform-specific agents in MetaAgent.yaml.
/// </summary>
public class DynamicIncidentManagementAgent
{
    private readonly ILogger<DynamicIncidentManagementAgent> _logger;
    private readonly IOptionsMonitor<IncidentManagementSettings> _incidentManagementSettingsOption;
    private IncidentManagementSettings _settings => _incidentManagementSettingsOption.CurrentValue;

    // Maps incident platforms to their YAML file names
    private readonly Dictionary<IncidentManagementType, string> _platformToYamlMapping = new()
    {
        { IncidentManagementType.PagerDuty, "PagerDutyIncidentManagementAgent.yaml" },
        { IncidentManagementType.ServiceNow, "ServiceNowIncidentManagementAgent.yaml" },
        { IncidentManagementType.Icm, "ICMIncidentManagementAgent.yaml" }
    };

    public DynamicIncidentManagementAgent(
        IOptionsMonitor<IncidentManagementSettings> incidentManagementSettingsOption,
        ILogger<DynamicIncidentManagementAgent> logger)
    {
        _incidentManagementSettingsOption = incidentManagementSettingsOption;

        _logger = logger;
    }

    /// <summary>
    /// Gets the appropriate incident management agent descriptor based on current configuration.
    /// Returns null if Type is None or if no YAML template is found.
    /// </summary>
    /// <returns>YamlAgentDescriptor with generic name, or null if not applicable</returns>
    public YamlAgentDescriptor? GetIncidentManagementAgentDescriptor()
    {
        // If incident management is disabled, return null
        if (_settings.Type == IncidentManagementType.None || _settings.Type == null)
        {
            _logger.LogInternalInformation("Incident management is disabled (Type=None).");
            return null;
        }

        // Get YAML file for the configured platform
        if (!_platformToYamlMapping.TryGetValue(_settings.Type.Value, out var yamlFileName))
        {
            _logger.LogInternalWarning("No YAML template found for incident platform: {Platform}", _settings.Type);
            return null;
        }

        try
        {
            // Build the file path following the AgentsV2 pattern
            var yamlPath = Path.Combine(AppContext.BaseDirectory, "AgentsV2", yamlFileName);

            if (!File.Exists(yamlPath))
            {
                _logger.LogInternalError("YAML file not found: {Path}", yamlPath);
                return null;
            }

            var yamlContent = File.ReadAllText(yamlPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var descriptor = deserializer.Deserialize<YamlAgentDescriptor>(yamlContent);

            // Override the name to be generic - this is the key magic!
            descriptor.Name = "incident_management_agent";

            // Update handoff description to be more generic but platform-aware
            descriptor.HandoffDescription = $"Handoff to manage {_settings.Type} incidents - acknowledge, add notes/discussions, resolve";

            _logger.LogInternalInformation("Loaded incident management agent for {Platform} as 'incident_management_agent'", _settings.Type);
            return descriptor;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to load YAML for {Platform}", _settings.Type);
            return null;
        }
    }
}
