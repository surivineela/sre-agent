namespace PerformanceIssues.Models
{
    public class CacheEntryRequest
    {
        public int SizeMB { get; set; }
    }

    public class CPUTaskRequest
    {
        public int Complexity { get; set; }
    }

    public class DataGenerationRequest
    {
        public int RecordCount { get; set; }
    }
}
