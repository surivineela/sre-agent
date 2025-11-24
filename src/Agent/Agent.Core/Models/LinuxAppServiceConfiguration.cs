namespace Agent.Core.Models;

/// <summary>
/// Represents The siteConfig of a Linux App Service.
/// </summary>
/// <param name="ResourceId">The Azure resource ID</param>
/// <param name="Name">The App Service name</param>
/// <param name="Location">The Azure region location</param>
/// <param name="LinuxFxVersion">The Linux framework version (e.g., "PYTHON|3.11", "NODE|18-lts")</param>
/// <param name="AppKind">The App Service kind (determines if it's a Linux service)</param>
public record LinuxAppServiceConfiguration(
    string ResourceId,
    string Name,
    string Location,
    string LinuxFxVersion,
    string AppKind);
