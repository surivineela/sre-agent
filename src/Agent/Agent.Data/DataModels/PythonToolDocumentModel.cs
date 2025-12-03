// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Data.Tools;
using Agent.Framework;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Python Function Tool storage
/// </summary>
public record PythonToolDocumentModel : ToolDocumentModel
{
    public PythonToolDocumentModel(ResourceMetadata metadata, PythonToolSpec spec)
        : base(metadata, spec)
    {
    }

    public new PythonToolSpec Spec => (PythonToolSpec)base.Spec;

    public override YamlToolDefinitionBase ToYamlToolDefinition()
    {
        return new PythonFunctionToolDefinition
        {
            Name = Name,
            Type = Type,
            Connector = Spec.Connector ?? string.Empty,
            Description = Spec.Description,
            Parameters = Spec.Parameters ?? new List<YamlParameter>(),
            Attributes = Spec.Attributes ?? new List<string>(),
            Metadata = Metadata.ToYamlMetadata(),
            FunctionCode = Spec.FunctionCode,
            TimeoutSeconds = Spec.TimeoutSeconds,
            Dependencies = Spec.Dependencies ?? new List<string>(),
        };
    }
}

/// <summary>
/// Python-specific tool spec
/// </summary>
public class PythonToolSpec : ToolSpec
{
    public string FunctionCode { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public List<string>? Dependencies { get; set; }
}
