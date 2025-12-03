// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Agent.Core.Helpers.JmesPath;

/// <summary>
/// Tree interpreter that evaluates an AST against JSON data.
/// </summary>
public class TreeInterpreter
{
    private readonly Dictionary<string, Func<JsonElement, JsonElement, bool>> _comparatorFuncs;
    private readonly HashSet<string> _equalityOps = new() { "eq", "ne" };
    private readonly Functions _functions;

    public TreeInterpreter(Functions? functions = null)
    {
        _functions = functions ?? new Functions();

        _comparatorFuncs = new Dictionary<string, Func<JsonElement, JsonElement, bool>>
        {
            { "eq", Equals },
            { "ne", (x, y) => !Equals(x, y) },
            { "lt", (x, y) => Compare(x, y) < 0 },
            { "gt", (x, y) => Compare(x, y) > 0 },
            { "lte", (x, y) => Compare(x, y) <= 0 },
            { "gte", (x, y) => Compare(x, y) >= 0 }
        };
    }

    public object? Visit(AstNode node, JsonElement value)
    {
        return node.Type switch
        {
            "subexpression" => VisitSubexpression(node, value),
            "field" => VisitField(node, value),
            "comparator" => VisitComparator(node, value),
            "current" => VisitCurrent(node, value),
            "expref" => VisitExpref(node, value),
            "function_expression" => VisitFunctionExpression(node, value),
            "filter_projection" => VisitFilterProjection(node, value),
            "flatten" => VisitFlatten(node, value),
            "identity" => VisitIdentity(node, value),
            "index" => VisitIndex(node, value),
            "index_expression" => VisitIndexExpression(node, value),
            "slice" => VisitSlice(node, value),
            "key_val_pair" => VisitKeyValPair(node, value),
            "literal" => VisitLiteral(node, value),
            "multi_select_dict" => VisitMultiSelectDict(node, value),
            "multi_select_list" => VisitMultiSelectList(node, value),
            "or_expression" => VisitOrExpression(node, value),
            "and_expression" => VisitAndExpression(node, value),
            "not_expression" => VisitNotExpression(node, value),
            "pipe" => VisitPipe(node, value),
            "projection" => VisitProjection(node, value),
            "value_projection" => VisitValueProjection(node, value),
            _ => throw new NotImplementedException($"Node type '{node.Type}' not implemented")
        };
    }

    private object? VisitSubexpression(AstNode node, JsonElement value)
    {
        object? result = value;
        foreach (var child in node.Children)
        {
            if (result is JsonElement jsonResult)
            {
                result = Visit(child, jsonResult);
            }
            else
            {
                return null;
            }
        }
        return result;
    }

    private object? VisitField(AstNode node, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fieldName = node.Value?.ToString() ?? "";
        if (value.TryGetProperty(fieldName, out var property))
        {
            return property;
        }

        return null;
    }

    private object? VisitComparator(AstNode node, JsonElement value)
    {
        var comparatorName = node.Value?.ToString() ?? "";
        var comparatorFunc = _comparatorFuncs[comparatorName];

        var left = Visit(node.Children[0], value);
        var right = Visit(node.Children[1], value);

        // Convert to JsonElement for comparison
        var leftElement = ToJsonElement(left);
        var rightElement = ToJsonElement(right);

        if (_equalityOps.Contains(comparatorName))
        {
            return comparatorFunc(leftElement, rightElement);
        }
        else
        {
            // Ordering operators only valid for numbers and strings
            if (!IsComparable(leftElement) || !IsComparable(rightElement))
            {
                return null;
            }
            return comparatorFunc(leftElement, rightElement);
        }
    }

    private object? VisitCurrent(AstNode node, JsonElement value)
    {
        return value;
    }

    private object? VisitExpref(AstNode node, JsonElement value)
    {
        return new ExpressionRef(node.Children[0], this);
    }

    private object? VisitFunctionExpression(AstNode node, JsonElement value)
    {
        var resolvedArgs = new List<object?>();
        foreach (var child in node.Children)
        {
            var current = Visit(child, value);
            resolvedArgs.Add(current);
        }
        return _functions.CallFunction(node.Value?.ToString() ?? "", resolvedArgs, this);
    }

    private object? VisitFilterProjection(AstNode node, JsonElement value)
    {
        var baseValue = Visit(node.Children[0], value);
        if (baseValue is not JsonElement baseElement || baseElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var comparatorNode = node.Children[2];
        var collected = new List<JsonElement>();

        foreach (var element in baseElement.EnumerateArray())
        {
            var result = Visit(comparatorNode, element);
            if (IsTrue(result))
            {
                var current = Visit(node.Children[1], element);
                if (current != null && !(current is JsonElement e && e.ValueKind == JsonValueKind.Null))
                {
                    collected.Add(ToJsonElement(current));
                }
            }
        }

        return CreateJsonArray(collected);
    }

    private object? VisitFlatten(AstNode node, JsonElement value)
    {
        var baseValue = Visit(node.Children[0], value);
        if (baseValue is not JsonElement baseElement || baseElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var mergedList = new List<JsonElement>();
        foreach (var element in baseElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                mergedList.AddRange(element.EnumerateArray());
            }
            else
            {
                mergedList.Add(element);
            }
        }

        return CreateJsonArray(mergedList);
    }

    private object? VisitIdentity(AstNode node, JsonElement value)
    {
        return value;
    }

    private object? VisitIndex(AstNode node, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var index = Convert.ToInt32(node.Value);
        var arrayLength = value.GetArrayLength();

        // Handle negative indices
        if (index < 0)
        {
            index = arrayLength + index;
        }

        if (index < 0 || index >= arrayLength)
        {
            return null;
        }

        return value[index];
    }

    private object? VisitIndexExpression(AstNode node, JsonElement value)
    {
        object? result = value;
        foreach (var child in node.Children)
        {
            if (result is JsonElement jsonResult)
            {
                result = Visit(child, jsonResult);
            }
            else
            {
                return null;
            }
        }
        return result;
    }

    private object? VisitSlice(AstNode node, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var start = node.Children[0].Value as int?;
        var end = node.Children[1].Value as int?;
        var step = node.Children[2].Value as int? ?? 1;

        if (step == 0)
        {
            throw new InvalidValueException("slice step cannot be 0");
        }

        var list = value.EnumerateArray().ToList();
        var length = list.Count;

        // Normalize start and end
        var normalizedStart = start.HasValue ? (start.Value < 0 ? Math.Max(0, length + start.Value) : Math.Min(start.Value, length)) : 0;
        var normalizedEnd = end.HasValue ? (end.Value < 0 ? Math.Max(0, length + end.Value) : Math.Min(end.Value, length)) : length;

        var result = new List<JsonElement>();

        if (step > 0)
        {
            for (int i = normalizedStart; i < normalizedEnd; i += step)
            {
                result.Add(list[i]);
            }
        }
        else if (step < 0)
        {
            // Reverse slice
            var sliceStart = start.HasValue ? (start.Value < 0 ? length + start.Value : start.Value) : length - 1;
            var sliceEnd = end.HasValue ? (end.Value < 0 ? length + end.Value : end.Value) : -1;

            for (int i = sliceStart; i > sliceEnd; i += step)
            {
                if (i >= 0 && i < length)
                {
                    result.Add(list[i]);
                }
            }
        }

        return CreateJsonArray(result);
    }

    private object? VisitKeyValPair(AstNode node, JsonElement value)
    {
        return Visit(node.Children[0], value);
    }

    private object? VisitLiteral(AstNode node, JsonElement value)
    {
        return node.Value;
    }

    private object? VisitMultiSelectDict(AstNode node, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var collected = new Dictionary<string, object?>();
        foreach (var child in node.Children)
        {
            var key = child.Value?.ToString() ?? "";
            collected[key] = Visit(child, value);
        }

        var json = JsonSerializer.Serialize(collected);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private object? VisitMultiSelectList(AstNode node, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var collected = new List<object?>();
        foreach (var child in node.Children)
        {
            collected.Add(Visit(child, value));
        }

        var json = JsonSerializer.Serialize(collected);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private object? VisitOrExpression(AstNode node, JsonElement value)
    {
        var matched = Visit(node.Children[0], value);
        if (IsFalse(matched))
        {
            matched = Visit(node.Children[1], value);
        }
        return matched;
    }

    private object? VisitAndExpression(AstNode node, JsonElement value)
    {
        var matched = Visit(node.Children[0], value);
        if (IsFalse(matched))
        {
            return matched;
        }
        return Visit(node.Children[1], value);
    }

    private object? VisitNotExpression(AstNode node, JsonElement value)
    {
        var originalResult = Visit(node.Children[0], value);

        // Special case for 0
        if (originalResult is int i && i == 0)
        {
            return false;
        }
        if (originalResult is JsonElement e && e.ValueKind == JsonValueKind.Number && e.GetDouble() == 0)
        {
            return false;
        }

        return !IsTrue(originalResult);
    }

    private object? VisitPipe(AstNode node, JsonElement value)
    {
        object? result = value;
        foreach (var child in node.Children)
        {
            if (result is JsonElement jsonResult)
            {
                result = Visit(child, jsonResult);
            }
            else
            {
                return null;
            }
        }
        return result;
    }

    private object? VisitProjection(AstNode node, JsonElement value)
    {
        var baseValue = Visit(node.Children[0], value);
        if (baseValue is not JsonElement baseElement || baseElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var collected = new List<JsonElement>();
        foreach (var element in baseElement.EnumerateArray())
        {
            var current = Visit(node.Children[1], element);
            if (current != null && !(current is JsonElement e && e.ValueKind == JsonValueKind.Null))
            {
                collected.Add(ToJsonElement(current));
            }
        }

        return CreateJsonArray(collected);
    }

    private object? VisitValueProjection(AstNode node, JsonElement value)
    {
        var baseValue = Visit(node.Children[0], value);
        if (baseValue is not JsonElement baseElement || baseElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var collected = new List<JsonElement>();
        foreach (var prop in baseElement.EnumerateObject())
        {
            var current = Visit(node.Children[1], prop.Value);
            if (current != null && !(current is JsonElement e && e.ValueKind == JsonValueKind.Null))
            {
                collected.Add(ToJsonElement(current));
            }
        }

        return CreateJsonArray(collected);
    }

    // Helper methods
    private bool IsFalse(object? value)
    {
        if (value == null) return true;
        if (value is bool b) return !b;
        if (value is string s) return s == "";
        if (value is JsonElement e)
        {
            return e.ValueKind switch
            {
                JsonValueKind.Null => true,
                JsonValueKind.False => true,
                JsonValueKind.String => e.GetString() == "",
                JsonValueKind.Array => e.GetArrayLength() == 0,
                JsonValueKind.Object => !e.EnumerateObject().Any(),
                _ => false
            };
        }
        return false;
    }

    private bool IsTrue(object? value)
    {
        return !IsFalse(value);
    }

    private bool Equals(JsonElement x, JsonElement y)
    {
        if (IsSpecialNumberCase(x, y) || IsSpecialNumberCase(y, x))
        {
            return false;
        }

        return JsonSerializer.Serialize(x) == JsonSerializer.Serialize(y);
    }

    private bool IsSpecialNumberCase(JsonElement x, JsonElement y)
    {
        // We need to special case comparing 0 or 1 to True/False.
        // While normally comparing any integer other than 0/1 to True/False
        // will always return False, 0/1 have special behavior in many languages:
        // In Python:
        // >>> 0 == True
        // False
        // >>> 0 == False
        // True
        // >>> 1 == True
        // True
        // >>> 1 == False
        // False
        // Also need to consider that:
        // >>> 0 in [True, False]
        // True
        // This function ensures JMESPath behavior matches the spec by treating
        // boolean comparisons with 0/1 as False.
        if (x.ValueKind == JsonValueKind.Number)
        {
            var num = x.GetDouble();
            if (num == 0.0 || num == 1.0)
            {
                return y.ValueKind is JsonValueKind.True or JsonValueKind.False;
            }
        }
        return false;
    }

    private int Compare(JsonElement x, JsonElement y)
    {
        if (x.ValueKind == JsonValueKind.Number && y.ValueKind == JsonValueKind.Number)
        {
            return x.GetDouble().CompareTo(y.GetDouble());
        }
        if (x.ValueKind == JsonValueKind.String && y.ValueKind == JsonValueKind.String)
        {
            return string.Compare(x.GetString(), y.GetString(), StringComparison.Ordinal);
        }
        return 0;
    }

    private bool IsComparable(JsonElement value)
    {
        // The spec doesn't officially support string types yet,
        // but enough people are relying on this behavior that
        // it's been added back. This should eventually become
        // part of the official spec.
        return value.ValueKind is JsonValueKind.Number or JsonValueKind.String;
    }

    private JsonElement ToJsonElement(object? obj)
    {
        if (obj is JsonElement e) return e;

        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private JsonElement CreateJsonArray(List<JsonElement> elements)
    {
        var json = JsonSerializer.Serialize(elements);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
