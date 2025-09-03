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
