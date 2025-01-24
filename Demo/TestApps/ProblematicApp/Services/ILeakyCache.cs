namespace PerformanceIssues.Serivces
{
    public interface ILeakyCache
    {
        Task<string> AddToCache(string key, int sizeInMb);
        int GetCacheSize();
    }
}
