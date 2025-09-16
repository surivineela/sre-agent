// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.ScheduledTasks.Services;

public interface IScheduledTaskManagementService
{
    Task<List<ScheduledTaskDocument>> ListScheduledTasks();
    Task<ScheduledTaskDocument?> GetScheduledTask(string taskId);
    Task<ScheduledTaskDocument> CreateScheduledTask(CreateScheduledTaskRequest request);
    Task<ScheduledTaskDocument> UpdateScheduledTask(string taskId, UpdateScheduledTaskRequest request);
    Task<bool> DeleteScheduledTask(string taskId);
    Task<bool> PauseScheduledTask(string taskId);
    Task<bool> ResumeScheduledTask(string taskId);
    Task<List<ScheduledTaskExecution>> GetTaskExecutionHistory(string taskId);
    Task<List<ScheduledTaskDocument>> GetTasksByThread(string threadId);
}