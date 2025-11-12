// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> NotDefaults<T>(this IEnumerable<T?> collection)
    {
        return collection.Where(e => e is not null && !EqualityComparer<T>.Default.Equals(e, default(T)))!;
    }
}
