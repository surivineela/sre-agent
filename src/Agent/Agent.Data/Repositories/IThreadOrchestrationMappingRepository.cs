// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.Repositories;

public interface IThreadOrchestrationMappingRepository
{
    Task<IEnumerable<ThreadOrchestrationMapping>> GetMappingsByThreadIdAsync(string threadId);
    Task<ThreadOrchestrationMapping> AddThreadMappingAsync(ThreadOrchestrationMapping mapping);
    Task<bool> RemoveThreadMappingAsync(string threadId);
    Task<bool> RemoveThreadMappingAsync(string threadId, string orchestrationInstanceId);
    Task<IEnumerable<ThreadOrchestrationMapping>> GetAllThreadMappingsAsync();
}
