// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using System.Text;
using Agent.Cli.Models;
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
            // Use Preserve so our visitor decides what to drop/keep
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .WithEmissionPhaseObjectGraphVisitor(args => new KeepImportantPropertiesVisitor(args.InnerVisitor))
            .Build();

        var yaml = serializer.Serialize(deploymentModel);
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
public class KeepImportantPropertiesVisitor : ChainedObjectGraphVisitor
{
    private static readonly HashSet<string> Important = new(StringComparer.OrdinalIgnoreCase)
    {
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
        // Keep important keys regardless of value
        if (Important.Contains(key.Name))
            return base.EnterMapping(key, value, context, serializer);

        // Otherwise, drop null/empty values
        if (value.Value == null) return false;
        if (value.Value is string s && string.IsNullOrEmpty(s)) return false;
        if (value.Value is ICollection col && col.Count == 0) return false;

        return base.EnterMapping(key, value, context, serializer);
    }
}
