using System.Diagnostics.CodeAnalysis;

namespace Agent.Core.Attributes;

/// <summary>
/// Provides additional information for semantic search fields.
/// </summary>
public class SemanticSearchAttribute : Attribute
{
    [SetsRequiredMembers]
    public SemanticSearchAttribute(SemanticSearchFieldType fieldType)
    {
        FieldType = fieldType;
    }

    public required SemanticSearchFieldType FieldType { get; init; }
}

public enum SemanticSearchFieldType
{
    TitleField,

    ContentField,
}
