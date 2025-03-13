using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Agent.Runtime.Communication;

public class InMemoryThreadOrchestrationManager : IThreadOrchestrationManager
{
    private readonly ConcurrentDictionary<string, ThreadOrchestrationMapping> _mappings = new();
    private readonly ILogger<InMemoryThreadOrchestrationManager> _logger;

    public InMemoryThreadOrchestrationManager(ILogger<InMemoryThreadOrchestrationManager> logger)
    {
        _logger = logger;
    }

    public Task<ThreadOrchestrationMapping?> GetMappingByThreadIdAsync(string threadId)
    {
        _mappings.TryGetValue(threadId, out var mapping);
        return Task.FromResult(mapping);
    }

    public Task<ThreadOrchestrationMapping?> GetMappingByInstanceIdAsync(string instanceId)
    {
        var mapping = _mappings.Values.FirstOrDefault(m => m.OrchestrationInstanceId == instanceId);
        return Task.FromResult(mapping);
    }

    public Task AddMappingAsync(ThreadOrchestrationMapping mapping)
    {
        if (!_mappings.TryAdd(mapping.ThreadId, mapping))
        {
            _logger.LogWarning("Failed to add mapping for ThreadId: {ThreadId}. It may already exist.", mapping.ThreadId);
        }
        return Task.CompletedTask;
    }

    public Task UpdateMappingAsync(ThreadOrchestrationMapping mapping)
    {
        _mappings[mapping.ThreadId] = mapping;
        return Task.CompletedTask;
    }

    public Task RemoveMappingAsync(string threadId)
    {
        _mappings.TryRemove(threadId, out _);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ThreadOrchestrationMapping>> GetAllMappingsAsync()
    {
        return Task.FromResult(_mappings.Values.AsEnumerable());
    }
}