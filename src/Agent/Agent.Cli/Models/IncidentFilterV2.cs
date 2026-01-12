// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Helpers;
using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML wrapper for incident filter configurations.
    /// Adds YAML envelope fields (api_version, kind) for file serialization.
    /// </summary>
    public class IncidentFilterV2 : ResourceModel
    {
        public IncidentFilterV2()
        {
            ApiVersion = YamlApiVersion.V2;
            Kind = ResourceKind.IncidentFilterV2;
        }

        /// <summary>
        /// Resource metadata (name, owner, tags).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel Metadata { get; set; } = new();

        /// <summary>
        /// Incident filter specification properties.
        /// </summary>
        [YamlMember(Alias = "spec", Order = 1)]
        public IncidentFilterSpecV2 Spec { get; set; } = new();

        /// <summary>
        /// Normalizes string properties to ensure clean YAML literal block formatting.
        /// </summary>
        public override void Normalize()
        {
            if (Spec != null)
            {
                Spec.TitleContains = NormalizeString(Spec.TitleContains);
                Spec.IncidentPlatform = IncidentFilterHelper.NormalizePlatform(Spec.IncidentPlatform);
            }
        }

        /// <summary>
        /// Parses a YAML string into an IncidentFilterV2 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed IncidentFilterV2 object</returns>
        public static IncidentFilterV2 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<IncidentFilterV2>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an IncidentFilterV2 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed IncidentFilterV2 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<IncidentFilterV2?> LoadYamlAsync(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                {
                    return null;
                }

                var yamlContent = await File.ReadAllTextAsync(fileName);
                return ParseYaml(yamlContent);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Incident filter specification for YAML configurations (V2).
    /// Contains filter properties used by the CLI for creating and managing incident filter configurations.
    /// </summary>
    public class IncidentFilterSpecV2
    {
        /// <summary>
        /// The incident management platform type (IcM, AzMonitor, PagerDuty, ServiceNow).
        /// </summary>
        [YamlMember(Alias = "incidentPlatform")]
        public string? IncidentPlatform { get; set; }

        /// <summary>
        /// The impacted service name to filter on.
        /// </summary>
        [YamlMember(Alias = "impactedService")]
        public string? ImpactedService { get; set; }

        /// <summary>
        /// The priority level to filter on.
        /// </summary>
        [YamlMember(Alias = "priority")]
        public string? Priority { get; set; }

        /// <summary>
        /// The incident type to filter on.
        /// </summary>
        [YamlMember(Alias = "incidentType")]
        public string? IncidentType { get; set; }

        /// <summary>
        /// The alert ID to filter on.
        /// </summary>
        [YamlMember(Alias = "alertId")]
        public string? AlertId { get; set; }

        /// <summary>
        /// Text that must be contained in the incident title.
        /// </summary>
        [YamlMember(Alias = "titleContains")]
        public string? TitleContains { get; set; }

        /// <summary>
        /// The agent mode to use for matching incidents.
        /// </summary>
        [YamlMember(Alias = "agentMode")]
        public string? AgentMode { get; set; }

        /// <summary>
        /// The agent that will handle matching incidents.
        /// </summary>
        [YamlMember(Alias = "handlingAgent")]
        public string? HandlingAgent { get; set; }

        /// <summary>
        /// The owning team ID to filter on.
        /// </summary>
        [YamlMember(Alias = "owningTeamId")]
        public string? OwningTeamId { get; set; }

        /// <summary>
        /// Maximum number of automated investigation attempts for recurring alerts before requesting user input.
        /// When an alert fires repeatedly and automated RCA fails to find a definitive root cause,
        /// the agent will ask the user for additional context after this many attempts.
        /// </summary>
        [YamlMember(Alias = "maxAutomatedInvestigationAttempts")]
        public int? MaxAutomatedInvestigationAttempts { get; set; }

        /// <summary>
        /// Whether deep investigation is enabled for matching incidents.
        /// </summary>
        [YamlMember(Alias = "deepInvestigationEnabled")]
        public bool? DeepInvestigationEnabled { get; set; }

        /// <summary>
        /// Whether this filter is enabled.
        /// </summary>
        [YamlMember(Alias = "isEnabled")]
        public bool? IsEnabled { get; set; }

        /// <summary>
        /// IcM-specific filter settings. Only set when incidentPlatform is "IcM".
        /// </summary>
        [YamlMember(Alias = "icmFilterSettings")]
        public IcmFilterSettingsV2? IcmFilterSettings { get; set; }

        /// <summary>
        /// Azure Monitor-specific filter settings. Only set when incidentPlatform is "AzMonitor".
        /// </summary>
        [YamlMember(Alias = "azMonitorFilterSettings")]
        public AzMonitorFilterSettingsV2? AzMonitorFilterSettings { get; set; }
    }

    /// <summary>
    /// ICM-specific filter settings for incident filters.
    /// </summary>
    public class IcmFilterSettingsV2
    {
        /// <summary>
        /// The ICM monitor ID to filter on.
        /// </summary>
        [YamlMember(Alias = "monitorId")]
        public string? MonitorId { get; set; }

        /// <summary>
        /// The creator of the incident to filter on.
        /// </summary>
        [YamlMember(Alias = "createdBy")]
        public string? CreatedBy { get; set; }
    }

    /// <summary>
    /// Azure Monitor-specific filter settings for incident filters.
    /// </summary>
    public class AzMonitorFilterSettingsV2
    {
        /// <summary>
        /// The target resource type to filter on.
        /// </summary>
        [YamlMember(Alias = "targetResourceType")]
        public string? TargetResourceType { get; set; }

        /// <summary>
        /// The target resource to filter on.
        /// </summary>
        [YamlMember(Alias = "targetResource")]
        public string? TargetResource { get; set; }
    }
}
