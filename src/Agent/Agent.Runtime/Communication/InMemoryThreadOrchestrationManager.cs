using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Agent.Runtime.Communication;

public class InMemoryThreadOrchestrationManager : IThreadOrchestrationManager
{
    // Changed to store multiple mappings per thread
    private readonly ConcurrentDictionary<string, List<ThreadOrchestrationMapping>> _mappingsByThread = new();
    // Secondary index for quick lookups by orchestration ID
    private readonly ConcurrentDictionary<string, ThreadOrchestrationMapping> _mappingsByOrchestration = new();
    private readonly ILogger<InMemoryThreadOrchestrationManager> _logger;

    public InMemoryThreadOrchestrationManager(ILogger<InMemoryThreadOrchestrationManager> logger)
    {
        _logger = logger;
    }

    // Returns all mappings for a given thread ID
    public Task<IEnumerable<ThreadOrchestrationMapping>> GetMappingsByThreadIdAsync(string threadId)
    {
        _mappingsByThread.TryGetValue(threadId, out var mappings);
        return Task.FromResult(mappings?.AsEnumerable() ?? Enumerable.Empty<ThreadOrchestrationMapping>());
    }

    public Task<ThreadOrchestrationMapping?> GetMappingByInstanceIdAsync(string instanceId)
    {
        _mappingsByOrchestration.TryGetValue(instanceId, out var mapping);
        return Task.FromResult(mapping);
    }

    public Task AddMappingAsync(ThreadOrchestrationMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.ThreadId) || string.IsNullOrEmpty(mapping.OrchestrationInstanceId))
        {
            _logger.LogWarning("Cannot add mapping with null or empty ThreadId or OrchestrationInstanceId");
            return Task.CompletedTask;
        }

        _mappingsByThread.AddOrUpdate(
            mapping.ThreadId,
            // If the thread ID doesn't exist, create a new list with this mapping
            _ => new List<ThreadOrchestrationMapping> { mapping },
            // If the thread ID exists, add this mapping to the existing list
            (_, existingMappings) =>
            {
                // Check if this orchestration already exists for the thread
                var existingMapping = existingMappings.FirstOrDefault(m =>
                    m.OrchestrationInstanceId == mapping.OrchestrationInstanceId);

                if (existingMapping != null)
                {
                    _logger.LogWarning("Mapping for ThreadId: {ThreadId} and OrchestrationInstanceId: {InstanceId} already exists.",
                        mapping.ThreadId, mapping.OrchestrationInstanceId);
                }
                else
                {
                    mapping.CreatedAt = DateTime.UtcNow;
                    existingMappings.Add(mapping);
                }

                return existingMappings;
            });

        // Update the orchestration index
        _mappingsByOrchestration[mapping.OrchestrationInstanceId] = mapping;

        return Task.CompletedTask;
    }

    public Task RemoveMappingAsync(string threadId, string orchestrationInstanceId)
    {
        if (_mappingsByThread.TryGetValue(threadId, out var mappings))
        {
            var mapping = mappings.FirstOrDefault(m => m.OrchestrationInstanceId == orchestrationInstanceId);
            if (mapping != null)
            {
                mappings.Remove(mapping);
                _mappingsByOrchestration.TryRemove(orchestrationInstanceId, out _);

                // If no mappings left for this thread, remove the thread entry
                if (mappings.Count == 0)
                {
                    _mappingsByThread.TryRemove(threadId, out _);
                }
            }
        }

        return Task.CompletedTask;
    }

    // For backward compatibility
    public Task RemoveMappingAsync(string threadId)
    {
        if (_mappingsByThread.TryRemove(threadId, out var mappings))
        {
            // Remove all orchestration mappings associated with this thread
            foreach (var mapping in mappings)
            {
                _mappingsByOrchestration.TryRemove(mapping.OrchestrationInstanceId, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<ThreadOrchestrationMapping>> GetAllMappingsAsync()
    {
        // Flatten the mappings from all threads
        var allMappings = _mappingsByThread.Values.SelectMany(list => list);
        return Task.FromResult(allMappings);
    }
}