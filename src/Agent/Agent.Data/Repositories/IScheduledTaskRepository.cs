// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Data.Repositories;

public interface IScheduledTaskRepository
{
    Task<ScheduledTaskDocument?> GetScheduledTaskAsync(string taskId);
    Task<List<ScheduledTaskDocument>> GetActiveScheduledTasksAsync();
    Task<List<ScheduledTaskDocument>> GetAllScheduledTasksAsync();
    Task<List<ScheduledTaskDocument>> GetScheduledTasksByThreadAsync(string threadId);
    Task<ScheduledTaskDocument> CreateScheduledTaskAsync(ScheduledTaskDocument task);
    Task<ScheduledTaskDocument> UpdateScheduledTaskAsync(ScheduledTaskDocument task);
    Task<bool> DeleteScheduledTaskAsync(string taskId);
    Task<List<ScheduledTaskDocument>> GetTasksDueForExecutionAsync(DateTime currentTime);
}
