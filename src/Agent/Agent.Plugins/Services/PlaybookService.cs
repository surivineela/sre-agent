using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Services;

/// <summary>
/// Service for loading and caching playbook content from markdown files
/// </summary>
public interface IPlaybookService
{
    Task<List<PlaybookInfo>> GetAvailablePlaybooksAsync(string category);
    Task<PlaybookContent> GetPlaybookContentAsync(string category, string playbookName);
}

/// <summary>
/// Implementation of playbook service with file-based loading and memory caching
/// </summary>
public class PlaybookService : IPlaybookService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlaybookService> _logger;
    private readonly string _playbooksBasePath;

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    public PlaybookService(IMemoryCache cache, ILogger<PlaybookService> logger)
    {
        _cache = cache;
        _logger = logger;
        _playbooksBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Playbooks");
    }

    public async Task<List<PlaybookInfo>> GetAvailablePlaybooksAsync(string category)
    {
        var cacheKey = $"playbooks_list_{category}";
        if (_cache.TryGetValue(cacheKey, out List<PlaybookInfo>? cachedPlaybooks))
        {
            return cachedPlaybooks!;
        }

        try
        {
            _logger.LogInternalInformation($"Loading playbooks for category: {category}");

            var categoryPath = Path.Combine(_playbooksBasePath, category);
            var metadataPath = Path.Combine(categoryPath, "playbooks.json");

            if (!File.Exists(metadataPath))
            {
                _logger.LogInternalWarning($"Playbooks metadata file not found: {metadataPath}");
                return new List<PlaybookInfo>();
            }

            var json = await File.ReadAllTextAsync(metadataPath);
            var metadata = JsonSerializer.Deserialize<List<PlaybookMetadata>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (metadata == null)
            {
                _logger.LogInternalWarning($"Failed to deserialize playbooks metadata from: {metadataPath}");
                return new List<PlaybookInfo>();
            }

            var playbooks = metadata.Select(m => new PlaybookInfo(
                Name: m.Name,
                Description: m.Description,
                Category: m.Category,
                Tags: m.Tags
            )).ToList();

            _cache.Set(cacheKey, playbooks, CacheExpiration);

            _logger.LogInternalInformation($"Loaded {playbooks.Count} playbooks for category: {category}");
            return playbooks;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error loading playbooks for category: {category}");
            return new List<PlaybookInfo>();
        }
    }

    public async Task<PlaybookContent> GetPlaybookContentAsync(string category, string playbookName)
    {
        var cacheKey = $"playbook_content_{category}_{playbookName}";

        if (_cache.TryGetValue(cacheKey, out PlaybookContent? cachedContent))
        {
            return cachedContent!;
        }

        try
        {
            _logger.LogInternalInformation($"Loading playbook content: {category}/{playbookName}");

            // First get the metadata to find the filename
            var metadata = await GetPlaybookMetadataAsync(category, playbookName);
            if (metadata == null)
            {
                return CreateNotFoundPlaybook(playbookName);
            }

            var categoryPath = Path.Combine(_playbooksBasePath, category);
            var filePath = Path.Combine(categoryPath, metadata.FileName);

            if (!File.Exists(filePath))
            {
                _logger.LogInternalWarning($"Playbook file not found: {filePath}");
                return CreateNotFoundPlaybook(playbookName);
            }

            var markdown = await File.ReadAllTextAsync(filePath);
            var content = ParseMarkdownPlaybook(markdown, metadata);

            _cache.Set(cacheKey, content, CacheExpiration);

            _logger.LogInternalInformation($"Loaded playbook content: {category}/{playbookName}");
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error loading playbook content: {category}/{playbookName}");
            return CreateNotFoundPlaybook(playbookName);
        }
    }

    private async Task<PlaybookMetadata?> GetPlaybookMetadataAsync(string category, string playbookName)
    {
        try
        {
            var categoryPath = Path.Combine(_playbooksBasePath, category);
            var metadataPath = Path.Combine(categoryPath, "playbooks.json");

            if (!File.Exists(metadataPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(metadataPath);
            var metadataList = JsonSerializer.Deserialize<List<PlaybookMetadata>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return metadataList?.FirstOrDefault(m => m.Name == playbookName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error loading playbook metadata: {category}/{playbookName}");
            return null;
        }
    }

    private static PlaybookContent ParseMarkdownPlaybook(string markdown, PlaybookMetadata metadata)
    {
        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var name = ExtractSection(lines, "# ") ?? metadata.Name;
        var description = ExtractSection(lines, "## Description") ?? metadata.Description;
        var prerequisites = ExtractListSection(lines, "## Prerequisites");
        var estimatedTime = ExtractSection(lines, "## Estimated Time") ?? "Unknown";
        var steps = ExtractStepsSection(lines);
        var summary = ExtractSection(lines, "## Summary") ?? "";

        return new PlaybookContent(
            Name: name,
            Description: description,
            Steps: steps,
            Prerequisites: prerequisites,
            EstimatedTime: estimatedTime,
            Summary: summary,
            Content: markdown
        );
    }

    private static string? ExtractSection(string[] lines, string sectionHeader)
    {
        var sectionStart = Array.FindIndex(lines, line => line.StartsWith(sectionHeader));
        if (sectionStart == -1) return null;

        var content = new List<string>();
        for (int i = sectionStart + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("## ") || line.StartsWith("# "))
                break;
            if (!string.IsNullOrWhiteSpace(line))
                content.Add(line);
        }

        return content.Count > 0 ? string.Join(" ", content) : null;
    }

    private static List<string> ExtractListSection(string[] lines, string sectionHeader)
    {
        var sectionStart = Array.FindIndex(lines, line => line.StartsWith(sectionHeader));
        if (sectionStart == -1) return new List<string>();

        var items = new List<string>();
        for (int i = sectionStart + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("## ") || line.StartsWith("# "))
                break;
            if (line.StartsWith("- "))
                items.Add(line.Substring(2).Trim());
        }

        return items;
    }

    private static List<string> ExtractStepsSection(string[] lines)
    {
        var stepsStart = Array.FindIndex(lines, line => line.StartsWith("## Steps"));
        if (stepsStart == -1) return new List<string>();

        var steps = new List<string>();
        var currentStep = new List<string>();

        for (int i = stepsStart + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("## ") && !line.StartsWith("### "))
                break;

            if (line.StartsWith("### "))
            {
                // Start of a new step
                if (currentStep.Count > 0)
                {
                    steps.Add(string.Join(" ", currentStep));
                    currentStep.Clear();
                }
                currentStep.Add(line.Substring(4).Trim()); // Remove "### "
            }
            else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("```"))
            {
                // Continue current step content (excluding code blocks)
                currentStep.Add(line);
            }
        }

        // Add the last step
        if (currentStep.Count > 0)
        {
            steps.Add(string.Join(" ", currentStep));
        }

        return steps;
    }
    private static PlaybookContent CreateNotFoundPlaybook(string playbookName)
    {
        return new PlaybookContent(
            Name: playbookName,
            Description: "Playbook not found",
            Steps: new List<string> { "Playbook content not available" },
            Prerequisites: new List<string>(),
            EstimatedTime: "Unknown",
            Summary: $"The requested playbook '{playbookName}' was not found in the available playbooks.",
            Content: $"# {playbookName}\n\nPlaybook not found. Please check the name and try again."
        );
    }
}
