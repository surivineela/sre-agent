// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class CosmosDbScheduledTaskRepository : IScheduledTaskRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbScheduledTaskRepository> _logger;
    private readonly string _databaseName;
    private readonly CosmosClient _client;

    public CosmosDbScheduledTaskRepository(CosmosClient cosmosClient, string databaseName, ILogger<CosmosDbScheduledTaskRepository> logger)
    {
        _logger = logger;
        _databaseName = databaseName;
        _client = cosmosClient;
        _container = _client.GetContainer(_databaseName, ScheduledTaskDocument.ContainerName);
    }

    public async Task<ScheduledTaskDocument?> GetScheduledTaskAsync(string taskId)
    {
        _logger.LogInternalInformation("Getting scheduled task with ID: {TaskId}", taskId);

        var iterator = _container.GetItemLinqQueryable<ScheduledTaskDocument>()
            .Where(doc => doc.DocumentType == "ScheduledTask" && doc.Id == taskId)
            .ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var task = response.FirstOrDefault();
            if (task != null)
            {
                _logger.LogInternalInformation("Found scheduled task with ID: {TaskId}", taskId);
                return task;
            }
        }

        _logger.LogInternalWarning("Scheduled task not found with ID: {TaskId}", taskId);
        return null;
    }

    public async Task<List<ScheduledTaskDocument>> GetActiveScheduledTasksAsync()
    {
        _logger.LogInternalInformation("Fetching all active scheduled tasks from Cosmos DB.");

        var iterator = _container.GetItemLinqQueryable<ScheduledTaskDocument>()
            .Where(doc => doc.DocumentType == "ScheduledTask" && doc.Status == ScheduledTaskStatus.Active)
            .OrderByDescending(doc => doc.CreatedAt)
            .ToFeedIterator();

        var tasks = new List<ScheduledTaskDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                tasks.Add(item);
            }
        }

        _logger.LogInternalInformation("Fetched {Count} active scheduled tasks from Cosmos DB.", tasks.Count);
        return tasks;
    }

    public async Task<List<ScheduledTaskDocument>> GetAllScheduledTasksAsync()
    {
        _logger.LogInternalInformation("Fetching all scheduled tasks (any status) from Cosmos DB.");

        var iterator = _container.GetItemLinqQueryable<ScheduledTaskDocument>()
            .Where(doc => doc.DocumentType == "ScheduledTask")
            .OrderByDescending(doc => doc.CreatedAt)
            .ToFeedIterator();

        var tasks = new List<ScheduledTaskDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                tasks.Add(item);
            }
        }

        _logger.LogInternalInformation("Fetched {Count} scheduled tasks (any status) from Cosmos DB.", tasks.Count);
        return tasks;
    }

    public async Task<List<ScheduledTaskDocument>> GetScheduledTasksByThreadAsync(string threadId)
    {
        _logger.LogInternalInformation("Fetching scheduled tasks for thread: {ThreadId}", threadId);

        var iterator = _container.GetItemLinqQueryable<ScheduledTaskDocument>()
            .Where(doc => doc.DocumentType == "ScheduledTask" && doc.ThreadId == threadId)
            .OrderByDescending(doc => doc.CreatedAt)
            .ToFeedIterator();

        var tasks = new List<ScheduledTaskDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                tasks.Add(item);
            }
        }

        _logger.LogInternalInformation("Fetched {Count} scheduled tasks for thread: {ThreadId}", tasks.Count, threadId);
        return tasks;
    }

    public async Task<ScheduledTaskDocument> CreateScheduledTaskAsync(ScheduledTaskDocument task)
    {
        _logger.LogInternalInformation("Creating scheduled task: {TaskName} with ID: {TaskId}", task.Name, task.Id);

        try
        {
            var response = await _container.CreateItemAsync(task, new PartitionKey(task.PartitionKey));
            _logger.LogInternalInformation("Successfully created scheduled task: {TaskId}", task.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error creating scheduled task: {TaskId}", task.Id);
            throw;
        }
    }

    public async Task<ScheduledTaskDocument> UpdateScheduledTaskAsync(ScheduledTaskDocument task)
    {
        _logger.LogInternalInformation("Updating scheduled task: {TaskId}", task.Id);

        try
        {
            var response = await _container.UpsertItemAsync(task, new PartitionKey(task.PartitionKey));
            _logger.LogInternalInformation("Successfully updated scheduled task: {TaskId}", task.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating scheduled task: {TaskId}", task.Id);
            throw;
        }
    }

    public async Task<bool> DeleteScheduledTaskAsync(string taskId)
    {
        _logger.LogInternalInformation("Deleting scheduled task: {TaskId}", taskId);

        try
        {
            await _container.DeleteItemAsync<ScheduledTaskDocument>(taskId, new PartitionKey(taskId));
            _logger.LogInternalInformation("Successfully deleted scheduled task: {TaskId}", taskId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Scheduled task not found for deletion: {TaskId}", taskId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error deleting scheduled task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<List<ScheduledTaskDocument>> GetTasksDueForExecutionAsync(DateTime currentTime)
    {
        _logger.LogInternalInformation("Fetching tasks due for execution before: {CurrentTime}", currentTime);

        // Get all active tasks and evaluate cron expressions at runtime
        var iterator = _container.GetItemLinqQueryable<ScheduledTaskDocument>()
            .Where(doc => doc.DocumentType == "ScheduledTask"
                && doc.Status == ScheduledTaskStatus.Active)
            .ToFeedIterator();

        var tasks = new List<ScheduledTaskDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                // Skip expired tasks
                if (item.EndTime.HasValue && currentTime > item.EndTime.Value)
                {
                    continue;
                }

                // Skip tasks that have reached max executions
                if (item.MaxExecutions.HasValue && item.ExecutionCount >= item.MaxExecutions.Value)
                {
                    continue;
                }

                // Skip tasks that haven't reached start time
                if (currentTime < item.StartTime)
                {
                    continue;
                }

                // Check if task is due for execution based on cron expression
                if (IsTaskDueForExecution(item, currentTime))
                {
                    tasks.Add(item);
                }
            }
        }

        _logger.LogInternalInformation("Found {Count} tasks due for execution", tasks.Count);
        return tasks;
    }

    private static bool IsTaskDueForExecution(ScheduledTaskDocument task, DateTime currentTime)
    {
        try
        {
            var schedule = NCrontab.CrontabSchedule.Parse(task.CronExpression);
            var baseTime = task.LastExecutionTime ?? task.StartTime;
            var nextExecution = schedule.GetNextOccurrence(baseTime);

            return nextExecution <= currentTime;
        }
        catch
        {
            // If cron expression is invalid, don't execute
            return false;
        }
    }
}
