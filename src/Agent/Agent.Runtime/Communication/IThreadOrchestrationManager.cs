// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Communication;

public interface IThreadOrchestrationManager
{
    Task<IEnumerable<ThreadOrchestrationMapping>> GetMappingsByThreadIdAsync(string threadId);
    Task AddMappingAsync(ThreadOrchestrationMapping mapping);
    Task AddMappingAsync(string threadId, string orchestrationInstanceId);

    Task RemoveMappingAsync(string threadId);
    Task RemoveMappingAsync(string threadId, string orchestrationInstanceId);
    Task<IEnumerable<ThreadOrchestrationMapping>> GetAllMappingsAsync();
}
