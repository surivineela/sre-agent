namespace Agent.Runtime.Communication;

public interface IThreadOrchestrationManager
{
    Task<ThreadOrchestrationMapping?> GetMappingByThreadIdAsync(string threadId);
    Task<ThreadOrchestrationMapping?> GetMappingByInstanceIdAsync(string instanceId);
    Task AddMappingAsync(ThreadOrchestrationMapping mapping);
    Task UpdateMappingAsync(ThreadOrchestrationMapping mapping);
    Task RemoveMappingAsync(string threadId);
    Task<IEnumerable<ThreadOrchestrationMapping>> GetAllMappingsAsync();
}