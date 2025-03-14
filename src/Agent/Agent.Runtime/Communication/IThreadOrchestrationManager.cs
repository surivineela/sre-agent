namespace Agent.Runtime.Communication;

public interface IThreadOrchestrationManager
{
    Task<IEnumerable<ThreadOrchestrationMapping>> GetMappingsByThreadIdAsync(string threadId);
    Task<ThreadOrchestrationMapping?> GetMappingByInstanceIdAsync(string instanceId);
    Task AddMappingAsync(ThreadOrchestrationMapping mapping);
    Task RemoveMappingAsync(string threadId);
    Task RemoveMappingAsync(string threadId, string orchestrationInstanceId);
    Task<IEnumerable<ThreadOrchestrationMapping>> GetAllMappingsAsync();
}