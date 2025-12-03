// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Collections.Generic;

namespace Agent.Core.Helpers.JmesPath;

/// <summary>
/// Represents an Abstract Syntax Tree node in a JMESPath expression.
/// </summary>
public class AstNode
{
    public string Type { get; set; } = string.Empty;
    public List<AstNode> Children { get; set; } = new();
    public object? Value { get; set; }

    public AstNode() { }

    public AstNode(string type, object? value = null, List<AstNode>? children = null)
    {
        Type = type;
        Value = value;
        Children = children ?? new List<AstNode>();
    }
}

/// <summary>
/// Factory methods for creating AST nodes.
/// </summary>
public static class Ast
{
    public static AstNode Comparator(string name, AstNode first, AstNode second)
    {
        return new AstNode("comparator", name, new List<AstNode> { first, second });
    }

    public static AstNode CurrentNode()
    {
        return new AstNode("current");
    }

    public static AstNode Expref(AstNode expression)
    {
        return new AstNode("expref", null, new List<AstNode> { expression });
    }

    public static AstNode FunctionExpression(string name, List<AstNode> args)
    {
        return new AstNode("function_expression", name, args);
    }

    public static AstNode Field(string name)
    {
        return new AstNode("field", name);
    }

    public static AstNode FilterProjection(AstNode left, AstNode right, AstNode comparator)
    {
        return new AstNode("filter_projection", null, new List<AstNode> { left, right, comparator });
    }

    public static AstNode Flatten(AstNode node)
    {
        return new AstNode("flatten", null, new List<AstNode> { node });
    }

    public static AstNode Identity()
    {
        return new AstNode("identity");
    }

    public static AstNode Index(int index)
    {
        return new AstNode("index", index);
    }

    public static AstNode IndexExpression(List<AstNode> children)
    {
        return new AstNode("index_expression", null, children);
    }

    public static AstNode KeyValPair(string keyName, AstNode node)
    {
        return new AstNode("key_val_pair", keyName, new List<AstNode> { node });
    }

    public static AstNode Literal(object? literalValue)
    {
        return new AstNode("literal", literalValue);
    }

    public static AstNode MultiSelectDict(List<AstNode> nodes)
    {
        return new AstNode("multi_select_dict", null, nodes);
    }

    public static AstNode MultiSelectList(List<AstNode> nodes)
    {
        return new AstNode("multi_select_list", null, nodes);
    }

    public static AstNode OrExpression(AstNode left, AstNode right)
    {
        return new AstNode("or_expression", null, new List<AstNode> { left, right });
    }

    public static AstNode AndExpression(AstNode left, AstNode right)
    {
        return new AstNode("and_expression", null, new List<AstNode> { left, right });
    }

    public static AstNode NotExpression(AstNode expr)
    {
        return new AstNode("not_expression", null, new List<AstNode> { expr });
    }

    public static AstNode Pipe(AstNode left, AstNode right)
    {
        return new AstNode("pipe", null, new List<AstNode> { left, right });
    }

    public static AstNode Projection(AstNode left, AstNode right)
    {
        return new AstNode("projection", null, new List<AstNode> { left, right });
    }

    public static AstNode Subexpression(List<AstNode> children)
    {
        return new AstNode("subexpression", null, children);
    }

    public static AstNode Slice(int? start, int? end, int? step)
    {
        // Python stores raw values in children: {"type": "slice", "children": [start, end, step]}
        // In C# we wrap them in placeholder nodes since Children is List<AstNode>
        // The values are accessed via node.Children[i].Value in TreeInterpreter
        return new AstNode("slice", null, new List<AstNode>
        {
            new AstNode("", start),  // Simple placeholder node holding start value
            new AstNode("", end),    // Simple placeholder node holding end value
            new AstNode("", step)    // Simple placeholder node holding step value
        });
    }

    public static AstNode ValueProjection(AstNode left, AstNode right)
    {
        return new AstNode("value_projection", null, new List<AstNode> { left, right });
    }
}
