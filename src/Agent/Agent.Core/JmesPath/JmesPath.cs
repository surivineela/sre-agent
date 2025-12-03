// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Text.Json;

namespace Agent.Core.Helpers.JmesPath;

/// <summary>
/// Main entry point for JMESPath query evaluation.
/// </summary>
public class JmesPath
{
    private readonly Parser _parser;
    private readonly TreeInterpreter _interpreter;

    /// <summary>
    /// Creates a new JmesPath instance.
    /// </summary>
    /// <param name="functions">Optional custom functions instance.</param>
    public JmesPath(Functions? functions = null)
    {
        _parser = new Parser();
        _interpreter = new TreeInterpreter(functions);
    }

    /// <summary>
    /// Compiles a JMESPath expression for later execution.
    /// </summary>
    /// <param name="expression">The JMESPath expression to compile.</param>
    /// <returns>A compiled result that can be searched.</returns>
    public static CompiledExpression Compile(string expression)
    {
        var parser = new Parser();
        var parsed = parser.Parse(expression);
        return new CompiledExpression(parsed);
    }

    /// <summary>
    /// Evaluates a JMESPath expression against JSON data.
    /// </summary>
    /// <param name="expression">The JMESPath expression.</param>
    /// <param name="data">The JSON data as JsonElement.</param>
    /// <returns>The result of the query.</returns>
    public static JsonElement Query(string expression, JsonElement data)
    {
        var instance = new JmesPath();
        return instance.Search(expression, data);
    }

    /// <summary>
    /// Evaluates a JMESPath expression against JSON data using this instance.
    /// </summary>
    /// <param name="expression">The JMESPath expression.</param>
    /// <param name="data">The JSON data as JsonElement.</param>
    /// <returns>The result of the query.</returns>
    public JsonElement Search(string expression, JsonElement data)
    {
        var parsed = _parser.Parse(expression);
        var result = _interpreter.Visit(parsed.Parsed, data);

        if (result is JsonElement element)
        {
            return element;
        }

        // Convert result to JsonElement
        var json = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// Clears the expression cache.
    /// </summary>
    public static void PurgeCache()
    {
        Parser.PurgeCache();
    }
}

/// <summary>
/// A compiled JMESPath expression that can be executed multiple times.
/// </summary>
public class CompiledExpression
{
    private readonly ParsedResult _parsed;
    private readonly TreeInterpreter _interpreter;

    internal CompiledExpression(ParsedResult parsed, Functions? functions = null)
    {
        _parsed = parsed;
        _interpreter = new TreeInterpreter(functions);
    }

    /// <summary>
    /// Searches the compiled expression against JSON data.
    /// </summary>
    /// <param name="data">The JSON data to search.</param>
    /// <returns>The result of the query.</returns>
    public JsonElement Search(JsonElement data)
    {
        var result = _interpreter.Visit(_parsed.Parsed, data);

        if (result is JsonElement element)
        {
            return element;
        }

        // Convert result to JsonElement
        var json = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// Gets the original expression string.
    /// </summary>
    public string Expression => _parsed.Expression;
}
