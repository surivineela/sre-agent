// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Agent.Core.Extensions;

public static class TaskExtensions
{

    public static Task<T?> IgnoreFailure<T>(this Task<T> task, ILogger? logger = null)
    {
        return task.ContinueWith(t =>
        {
            try
            {
                return t.Result;
            }
            catch (Exception e)
            {
                logger?.LogError(e, "Ignored task exception: {Exception}", e.ToString());
            }
            return default(T);
        }, TaskContinuationOptions.ExecuteSynchronously);
    }

    public static async Task<IEnumerable<T>> IgnoreAndFilterFailures<T>(this IEnumerable<Task<T>> collection, ILogger? logger = null)
    {
        return (await collection.Select(t => IgnoreFailure(t, logger)).WhenAll()).NotDefaults();
    }

    public static Task<T[]> WhenAll<T>(this IEnumerable<Task<T>> collection)
    {
        return Task.WhenAll(collection);
    }
}
