using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class TimerSettings
    {
        [Required]
        public int BackgroundCrawlIntervalInMinutes { get; set; } = 10;

        [Required]
        public int BestPracticeScanIntervalInMinutes { get; set; } = 5;
    }
}
