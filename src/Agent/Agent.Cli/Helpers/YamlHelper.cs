// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using System.Text;
using Agent.Cli.Models;
using Agent.Cli.Services;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Framework.Skills;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace Agent.Cli.Helpers;

public static class YamlHelper
{
    public static void WriteYamlFile(string folder, string name, Dictionary<string, object> data)
    {
        Directory.CreateDirectory(folder);
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(data);
        File.WriteAllText(Path.Combine(folder, $"{name}.yaml"), yaml, new UTF8Encoding(false));
    }

    /// <summary>
    /// Writes an agent configuration to a YAML file using the v2 API structure.
    /// </summary>
    /// <param name="folder">Folder to write the YAML file to</param>
    /// <param name="name">Name of the agent (used for filename)</param>
    /// <param name="agentSpec">Agent specification containing all configuration</param>
    /// <param name="metadata">Optional resource metadata (owner, tags, version, timestamps)</param>
    public static void WriteAgentYamlFile(string folder, string name, AgentSpec agentSpec, ResourceMetadata? metadata = null)
    {
        Directory.CreateDirectory(folder);

        var yamlModel = new AgentYamlModel
        {
            Spec = agentSpec,
            Metadata = metadata ?? new ResourceMetadata()
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .Build();

        var yaml = serializer.Serialize(yamlModel);
        File.WriteAllText(Path.Combine(folder, $"{name}.yaml"), yaml, new UTF8Encoding(false));
    }

    public static void WriteToolYamlFile(string folder, string name, YamlToolDefinitionBase tool, Framework.YamlMetadata? documentMetadata = null)
    {
        Directory.CreateDirectory(folder);

        // Create the deployment wrapper using the generic structure
        var deploymentModel = new StructuredToolListYaml
        {
            // Note: tool list is actually a single tool, but to keep it how the api expects right now, rename later
            Kind = "ToolList",
            Metadata = documentMetadata != null ?
                new Services.YamlMetadata
                {
                    Owner = documentMetadata.Owner ?? string.Empty,
                    Version = documentMetadata.Version ?? string.Empty,
                    Tags = documentMetadata.Tags?.ToList() ?? [],
                    UpdatedAt = documentMetadata.UpdatedAt?.ToString() ?? string.Empty,
                    CreatedAt = documentMetadata.CreatedAt?.ToString() ?? string.Empty
                } :
                new Services.YamlMetadata(),
            Spec = new ToolListSpec { Tools = [tool] }
        };

        DebugLogger.Debug("WriteToolYamlFile", $"Created deployment model - Kind: {deploymentModel.Kind}, ApiVersion: {deploymentModel.ApiVersion}");

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .WithEmissionPhaseObjectGraphVisitor(args => new ToolMetadataFilterVisitor(args.InnerVisitor))
            .Build();

        var yaml = serializer.Serialize(deploymentModel);

        DebugLogger.Debug("WriteToolYamlFile", $"Generated YAML length: {yaml.Length}");
        DebugLogger.Debug("WriteToolYamlFile", $"YAML preview (first 200 chars): {yaml.Substring(0, Math.Min(200, yaml.Length))}");

        File.WriteAllText(Path.Combine(folder, $"{name}.yaml"), yaml, new UTF8Encoding(false));
    }

    public static IDeserializer CreateCamelCaseDeserializer() =>
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

    /// <summary>
    /// Recursively prunes null/empty values from arbitrary objects (Dictionary/List graphs).
    /// Keeps non-empty strings, non-empty collections, and leaves other scalars untouched.
    /// Useful for slimming down tool YAMLs fetched from server payloads.
    /// </summary>
    public static object? PruneEmptyNodes(object? node)
    {
        if (node == null) return null;

        // Strings: drop if null/empty/whitespace
        if (node is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        // IDictionary: process each entry and drop null/empty results
        if (node is IDictionary dict)
        {
            var result = new Dictionary<object, object?>();
            foreach (DictionaryEntry entry in dict)
            {
                var pruned = PruneEmptyNodes(entry.Value);
                if (pruned == null) continue;

                // Skip empty dictionaries/lists
                if (pruned is IDictionary pd && pd.Count == 0) continue;
                if (pruned is IEnumerable pe && !(pruned is string))
                {
                    // materialize once
                    var hasAny = pe.Cast<object?>().Any();
                    if (!hasAny) continue;
                }
                result[entry.Key] = pruned;
            }
            return result.Count == 0 ? null : result;
        }

        // IEnumerable (excluding string): prune each item
        if (node is IEnumerable enumerable && node is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                var pruned = PruneEmptyNodes(item);
                if (pruned == null) continue;
                if (pruned is IDictionary pd && pd.Count == 0) continue;
                if (pruned is IEnumerable pe && pruned is not string)
                {
                    var hasAny = pe.Cast<object?>().Any();
                    if (!hasAny) continue;
                }
                list.Add(pruned);
            }
            return list.Count == 0 ? null : list;
        }

        // Other scalars: keep as-is
        return node;
    }

    #region Skill Helper Methods

    /// <summary>
    /// Validates if a directory contains the required skill files (metadata.yaml and SKILL.md).
    /// </summary>
    /// <param name="directoryPath">Path to the skill directory</param>
    /// <returns>True if the directory contains required skill files, false otherwise</returns>
    public static bool IsValidSkillDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        var metadataPath = Path.Combine(directoryPath, "metadata.yaml");
        var skillMdPath = Path.Combine(directoryPath, "SKILL.md");

        return File.Exists(metadataPath) && File.Exists(skillMdPath);
    }

    /// <summary>
    /// Saves a SkillSpec to a directory with metadata.yaml, SKILL.md, and additional files.
    /// </summary>
    /// <param name="directoryPath">Path to save the skill</param>
    /// <param name="skillSpec">The skill specification to save</param>
    public static async Task SaveSkillToDirectory(string directoryPath, SkillSpec skillSpec)
    {
        Directory.CreateDirectory(directoryPath);

        // Write metadata.yaml (contains name, description, tools from YamlSkillDescriptor)
        var metadataPath = Path.Combine(directoryPath, "metadata.yaml");
        var metadata = new YamlSkillDescriptor
        {
            Name = skillSpec.Name,
            Description = skillSpec.Description,
            Tools = skillSpec.Tools ?? []
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        var metadataYaml = serializer.Serialize(metadata);
        await File.WriteAllTextAsync(metadataPath, metadataYaml, Encoding.UTF8);

        // Write SKILL.md (main skill content)
        var skillMdPath = Path.Combine(directoryPath, "SKILL.md");
        await File.WriteAllTextAsync(skillMdPath, skillSpec.SkillMdContent ?? string.Empty, Encoding.UTF8);

        // Write additional files if any
        if (skillSpec.AdditionalFiles != null)
        {
            foreach (var additionalFile in skillSpec.AdditionalFiles)
            {
                var filePath = Path.Combine(directoryPath, additionalFile.FilePath);
                await File.WriteAllTextAsync(filePath, additionalFile.Content ?? string.Empty, Encoding.UTF8);
            }
        }
    }

    /// <summary>
    /// Reads a skill from a directory and converts it to YAML format for uploading.
    /// </summary>
    /// <param name="directoryPath">Path to the skill directory</param>
    /// <returns>YAML content representing the skill deployment</returns>
    public static async Task<string> ReadSkillFromDirectory(string directoryPath)
    {
        if (!IsValidSkillDirectory(directoryPath))
        {
            throw new InvalidOperationException($"Invalid skill directory: {directoryPath}. Must contain 'metadata.yaml' and 'SKILL.md'");
        }

        // Read metadata.yaml
        var metadataPath = Path.Combine(directoryPath, "metadata.yaml");
        var metadataYaml = await File.ReadAllTextAsync(metadataPath, Encoding.UTF8);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var skillDescriptor = deserializer.Deserialize<YamlSkillDescriptor>(metadataYaml);

        // Read SKILL.md
        var skillMdPath = Path.Combine(directoryPath, "SKILL.md");
        var skillMdContent = await File.ReadAllTextAsync(skillMdPath, Encoding.UTF8);

        // Find additional files (all .md files except SKILL.md and metadata.yaml)
        var additionalFiles = new List<SkillSubFile>();
        var allFiles = Directory.GetFiles(directoryPath, "*.md", SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(file, Encoding.UTF8);
            additionalFiles.Add(new SkillSubFile
            {
                FileName = fileName,
                FilePath = Path.GetRelativePath(directoryPath, file), // Relative path within skill directory
                Content = content
            });
        }

        // Create skill deployment model using proper types
        var skillSpec = new SkillSpec
        {
            Name = skillDescriptor?.Name ?? Path.GetFileName(directoryPath),
            Description = skillDescriptor?.Description ?? string.Empty,
            Tools = skillDescriptor?.Tools ?? [],
            SkillMdContent = skillMdContent,
            AdditionalFiles = additionalFiles
        };

        var skillDeployment = new CliSkillDeploymentModel
        {
            ApiVersion = "azuresre.ai/v1",
            Kind = "Skill",
            Metadata = new Framework.YamlMetadata(),
            Spec = skillSpec
        };

        // Serialize to YAML
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        return serializer.Serialize(skillDeployment);
    }

    /// <summary>
    /// Local deployment model for CLI skill serialization (mirrors Agent.Web.Models.ExtendedAgents.SkillDeploymentModel)
    /// </summary>
    private class CliSkillDeploymentModel
    {
        [YamlMember(Alias = "api_version")]
        public string ApiVersion { get; set; } = "azuresre.ai/v1";

        [YamlMember(Alias = "kind")]
        public string Kind { get; set; } = "Skill";

        [YamlMember(Alias = "metadata")]
        public Framework.YamlMetadata Metadata { get; set; } = new();

        [YamlMember(Alias = "spec")]
        public SkillSpec Spec { get; set; } = new();
    }

    #endregion
}

/// <summary>
/// Keeps specific keys even when they’re default/null; drops null/empty for others.
/// </summary>

/// <summary>
/// Visitor that filters out metadata from tool specifications while preserving structural metadata.
/// Extends KeepImportantPropertiesVisitor with tool-specific filtering.
/// </summary>
public class ToolMetadataFilterVisitor : ChainedObjectGraphVisitor
{
    private readonly KeepImportantPropertiesVisitor _baseVisitor;
    private bool _inToolsArray = false;

    public ToolMetadataFilterVisitor(IObjectGraphVisitor<IEmitter> next) : base(next)
    {
        _baseVisitor = new KeepImportantPropertiesVisitor(next);
    }

    public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context, ObjectSerializer serializer)
    {
        // Track when we're in the tools array
        if (key.Name.Equals("tools", StringComparison.OrdinalIgnoreCase))
        {
            _inToolsArray = true;
        }
        // Reset when we encounter top-level fields (exiting tools context)
        else if (!_inToolsArray || (key.Name.Equals("api_version", StringComparison.OrdinalIgnoreCase) ||
                                   key.Name.Equals("kind", StringComparison.OrdinalIgnoreCase) ||
                                   key.Name.Equals("spec", StringComparison.OrdinalIgnoreCase)))
        {
            _inToolsArray = false;
        }

        // Filter out metadata within tools (but allow top-level metadata)
        if (_inToolsArray && key.Name.Equals("metadata", StringComparison.OrdinalIgnoreCase))
        {
            return false; // Skip tool-level metadata
        }

        // Delegate to base visitor for all other logic
        return _baseVisitor.EnterMapping(key, value, context, serializer);
    }

    // Note: ChainedObjectGraphVisitor doesn't have ExitMapping to override
    // We'll track the depth and state manually in EnterMapping
}

public class KeepImportantPropertiesVisitor : ChainedObjectGraphVisitor
{
    private static readonly HashSet<string> StructuralFields = new(StringComparer.OrdinalIgnoreCase)
    {
        // Document structure - always keep at top level
        "api_version",
        "kind",
        "metadata",
        "spec"
    };

    private static readonly HashSet<string> AlwaysPreserve = new(StringComparer.OrdinalIgnoreCase)
    {
        // Important content fields - keep even if empty/default
        "custom_reflection_note",
        "temperature",
        "handoff_description",
        "handoffs",
        "tools",
        "connectors",
        "allow_parallel_tool_calls"
    };

    public KeepImportantPropertiesVisitor(IObjectGraphVisitor<IEmitter> next) : base(next) { }

    // NOTE: updated signature to include ObjectSerializer
    public override bool EnterMapping(
        IPropertyDescriptor key,
        IObjectDescriptor value,
        IEmitter context,
        ObjectSerializer serializer)
    {
        // Always preserve structural fields at document root level
        if (StructuralFields.Contains(key.Name))
            return base.EnterMapping(key, value, context, serializer);

        // Always preserve important content fields
        if (AlwaysPreserve.Contains(key.Name))
            return base.EnterMapping(key, value, context, serializer);

        // Drop null/empty values for other fields
        if (value.Value == null) return false;
        if (value.Value is string s && string.IsNullOrEmpty(s)) return false;
        if (value.Value is ICollection col && col.Count == 0) return false;

        return base.EnterMapping(key, value, context, serializer);
    }

}
