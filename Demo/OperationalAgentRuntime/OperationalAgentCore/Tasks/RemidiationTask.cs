namespace OperationalAgentCore
{
    public class RemediationTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); 
        public string ResourceId { get; set; } 
        public string CronExpression { get; set; } 
        public string Description { get; set; } 
        public TaskStatus Status { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
        public DateTime? LastExecuted { get; set; } 
        public DateTime? LastProgressNotification { get; set; } 
    }
}
