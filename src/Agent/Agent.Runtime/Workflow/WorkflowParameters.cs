// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;

namespace Agent.Runtime.Workflow;

/// <summary>
/// Helper class for managing workflow parameters with type-safe access and JSON serialization support.
/// </summary>
public class WorkflowParameters
{
    private readonly Dictionary<string, string> _parameters = new();

    public WorkflowParameters()
    {
    }

    public WorkflowParameters(Dictionary<string, string> parameters)
    {
        _parameters = new Dictionary<string, string>(parameters);
    }

    /// <summary>
    /// Gets a parameter value as a string.
    /// </summary>
    public string? GetString(string key)
    {
        return _parameters.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Sets a parameter value.
    /// </summary>
    public void SetString(string key, string value)
    {
        _parameters[key] = value;
    }

    /// <summary>
    /// Gets a parameter value and deserializes it from JSON.
    /// </summary>
    public T? GetObject<T>(string key) where T : class
    {
        if (!_parameters.TryGetValue(key, out var value))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sets a parameter value by serializing an object to JSON.
    /// </summary>
    public void SetObject<T>(string key, T value) where T : class
    {
        _parameters[key] = JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Merges another set of parameters into this instance.
    /// </summary>
    public void Merge(Dictionary<string, string> otherParameters)
    {
        foreach (var kvp in otherParameters)
        {
            _parameters[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Merges another WorkflowParameters instance into this instance.
    /// </summary>
    public void Merge(WorkflowParameters other)
    {
        Merge(other.ToDictionary());
    }

    /// <summary>
    /// Returns all parameters as a dictionary.
    /// </summary>
    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>(_parameters);
    }

    /// <summary>
    /// Checks if a parameter exists.
    /// </summary>
    public bool ContainsKey(string key)
    {
        return _parameters.ContainsKey(key);
    }

    /// <summary>
    /// Gets all parameter keys.
    /// </summary>
    public IEnumerable<string> Keys => _parameters.Keys;

    /// <summary>
    /// Gets the number of parameters.
    /// </summary>
    public int Count => _parameters.Count;

    /// <summary>
    /// Direct access to the underlying parameter dictionary for backwards compatibility.
    /// </summary>
    public Dictionary<string, string> Values => _parameters;
}
