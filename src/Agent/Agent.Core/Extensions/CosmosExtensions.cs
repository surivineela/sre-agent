using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Linq;

namespace Agent.Core.Extensions;
public static class CosmosExtensions
{
    public async static Task<List<T>> ToListAsync<T>(this IQueryable<T> queryable)
    {
        var iterator = queryable.ToFeedIterator();
        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }
}
