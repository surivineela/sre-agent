using System.ComponentModel;

namespace Agent.Core.Models;

/// <summary>
/// Metadata for a connected integration, including its name, whether it’s enabled,
/// and a little detail about how it’s configured (or why it isn’t).
/// </summary>
[Description("Holds the name, active status, and configuration details of an external integration.")]
public class IntegrationInfo
{
    /// <summary>
    /// The human‑readable name of the integration 
    /// (e.g. “Dashboard”, “IncidentManagement”, “AppInsights”).
    /// </summary>
    [Description("Human-readable integration name (e.g. Dashboard, IncidentManagement, AppInsights).")]
    public string Name { get; set; } = default!;

    /// <summary>
    /// Whether this integration is currently active (true if all required settings are present).
    /// </summary>
    [Description("True if this integration is properly configured and active; otherwise false.")]
    public bool IsActive { get; set; }

    /// <summary>
    /// A short description of how it’s configured (e.g. URL, key) or what’s missing.
    /// </summary>
    [Description("Short details on the integration’s configuration (e.g. URLs, keys) or missing settings.")]
    public string Details { get; set; } = default!;
}
