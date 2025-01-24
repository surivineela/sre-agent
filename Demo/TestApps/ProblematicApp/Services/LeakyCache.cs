using System.Collections.Concurrent;

namespace PerformanceIssues.Serivces
{
    public class LeakyCache : ILeakyCache
    {
        private static readonly ConcurrentDictionary<string, byte[]> _cache = new();
        private static readonly Random _random = new();

        public async Task<string> AddToCache(string key, int sizeInMb)
        {
            // Intentionally creating large byte arrays and storing them indefinitely
            byte[] data = new byte[sizeInMb * 1024 * 1024];
            _random.NextBytes(data);

            // Simulate some async work
            await Task.Delay(100);

            _cache.TryAdd(key, data);
            return key;
        }

        public int GetCacheSize() => _cache.Count;
    }
}
