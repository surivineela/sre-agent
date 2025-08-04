using System.Collections.Generic;

namespace Agent.Plugins.Models;

/// <summary>
/// Playbook information
/// </summary>
public record PlaybookInfo(
    string Name,
    string Description,
    string Category,
    List<string> Tags);

/// <summary>
/// Playbook content
/// </summary>
public record PlaybookContent(
    string Name,
    string Description,
    List<string> Steps,
    List<string> Prerequisites,
    string EstimatedTime,
    string Summary,
    string Content);

/// <summary>
/// Represents metadata about an available playbook from JSON configuration
/// </summary>
public record PlaybookMetadata(
    string Name,
    string Description,
    string Category,
    List<string> Tags,
    string FileName
);
