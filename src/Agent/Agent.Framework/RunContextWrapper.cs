// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class RunContextWrapper<TContext>
{
    public TContext? Context { get; }
    public Dictionary<string, object> Metadata { get; } = new();
    public UsageDetails TotalUsageDetails { get; } = new();
    public UsageDetails CurrentStepUsageDetails { get; set; } = new();

    public RunContextWrapper(TContext? context)
    {
        Context = context;
    }

    public T GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }
            throw new InvalidCastException($"Metadata value for key '{key}' is not of type {typeof(T)}");
        }
        throw new KeyNotFoundException($"Metadata key '{key}' not found");
    }

    public void SetMetadata<T>(string key, T value)
    {
        Metadata[key] = value!;
    }
}
