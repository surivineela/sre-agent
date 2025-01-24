namespace OperationalAgentRuntime.Models
{
    public class TrackedAgentOperation
    {
        public required Guid Id { get; set; }
        public required string OperationName { get; set; }
        public required string[] Annotations { get; set; }
        public required DateTime CreatedTime { get; set; }
        public string? Approver { get; set; }
    }
}
