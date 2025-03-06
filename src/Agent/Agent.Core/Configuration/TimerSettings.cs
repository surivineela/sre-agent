using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class TimerSettings
    {
        [Required]
        public int BackgroundCrawlIntervalInMinutes { get; set; } = 30;

        [Required]
        public int BestPracticeScanIntervalInMinutes { get; set; } = 5;
    }
}
