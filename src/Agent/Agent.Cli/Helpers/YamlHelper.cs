// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using System.Text;
using Agent.Cli.Models;
using Agent.Cli.Services;
using Agent.Framework;
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

    public static void WriteAgentYamlFile(string folder, string name, YamlAgentDescriptor agent)
    {
        Directory.CreateDirectory(folder);

        var deploymentModel = new AgentDeploymentModel { Spec = agent };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .WithEmissionPhaseObjectGraphVisitor(args => new KeepImportantPropertiesVisitor(args.InnerVisitor))
            .Build();

        var yaml = serializer.Serialize(deploymentModel);
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
