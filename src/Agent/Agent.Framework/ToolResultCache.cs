// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class ToolResultCache
{
    private readonly ConcurrentDictionary<string, object?> _cache = new();

    public void Add(FunctionCallContent functionCall, object? result)
    {
        var key = GenerateCacheKey(functionCall.Name, functionCall.Arguments);
        if (!_cache.ContainsKey(key))
        {
            _cache[key] = result;
        }
    }

    public bool TryGetValue(FunctionCallContent functionCall, out object? result)
    {
        var key = GenerateCacheKey(functionCall.Name, functionCall.Arguments);
        if (_cache.TryGetValue(key, out var cachedResult))
        {
            result = cachedResult;
            return true;
        }
        result = null;
        return false;
    }

    public void Clear()
    {
        _cache.Clear();
    }

    private static string GenerateCacheKey(string toolName, IDictionary<string, object?>? parameters)
    {
        // the order of the parameters shouldn't matter since its serialized to JSON and then hashed
        var parametersJson = parameters == null ? "null" : JsonSerializer.Serialize(parameters, JsonSerializerOptions.Web);
        var combined = $"{toolName}:{parametersJson}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hashBytes);
    }
}
