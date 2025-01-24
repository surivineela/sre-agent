using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Cli.DemoExec.Tasks;
using System.Text.Json;

public class TaskClient : ITaskClient
{
    private readonly string _filePath;
    private readonly object _fileLock = new object();
    private readonly ILogger<TaskClient> _logger;

    public TaskClient(ILogger<TaskClient> logger)
    {
        _filePath = Path.Combine("C:\\Test", "remediation_tasks.json");
        _logger = logger;
    }

    public async Task<List<RemediationTask>> GetPendingRemediationsAsync()
    {
        if (!File.Exists(_filePath))
            return new List<RemediationTask>();

        lock (_fileLock)
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                var tasks = JsonSerializer.Deserialize<List<RemediationTask>>(json) ?? new List<RemediationTask>();

                // Example: We only return tasks in 'Created' status. Adjust as needed.
                return tasks.Where(t => t.Status == TaskStatus.Created).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading tasks file");
                return new List<RemediationTask>();
            }
        }
    }

    public async Task ScheduleRemediationAsync(RemediationTask task)
    {
        lock (_fileLock)
        {
            try
            {
                var tasks = new List<RemediationTask>();
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    tasks = JsonSerializer.Deserialize<List<RemediationTask>>(json) ?? new List<RemediationTask>();
                }

                // Check if a task with the same Id exists
                var existingIndex = tasks.FindIndex(t => t.Id == task.Id);
                if (existingIndex >= 0)
                {
                    tasks[existingIndex] = task;
                }
                else
                {
                    tasks.Add(task);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_filePath, JsonSerializer.Serialize(tasks, options));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing task to file");
                throw;
            }
        }
    }

    /// <summary>
    /// Delete a remediation task by ID from the JSON file.
    /// </summary>
    public async Task DeleteRemediationAsync(string id)
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(_filePath))
                    return; // Nothing to delete

                var json = File.ReadAllText(_filePath);
                var tasks = JsonSerializer.Deserialize<List<RemediationTask>>(json) ?? new List<RemediationTask>();

                // Remove the task with the matching ID
                var existingTask = tasks.FirstOrDefault(t => t.Id == id);
                if (existingTask != null)
                {
                    tasks.Remove(existingTask);

                    // Write the updated list back to file
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(_filePath, JsonSerializer.Serialize(tasks, options));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting task {id}");
                throw;
            }
        }
    }

    /// <summary>
    /// Update an existing remediation task in the JSON file by matching its ID.
    /// </summary>
    public async Task UpdateRemediationAsync(RemediationTask updatedTask)
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(_filePath))
                    return; // No file = no tasks to update

                var json = File.ReadAllText(_filePath);
                var tasks = JsonSerializer.Deserialize<List<RemediationTask>>(json) ?? new List<RemediationTask>();

                var existingTask = tasks.FirstOrDefault(t => t.Id == updatedTask.Id);
                if (existingTask != null)
                {
                    existingTask.ResourceId = updatedTask.ResourceId;
                    existingTask.CronExpression = updatedTask.CronExpression;
                    existingTask.Description = updatedTask.Description;
                    existingTask.Status = updatedTask.Status;
                    existingTask.LastExecuted = updatedTask.LastExecuted;

                    // Write the updated list back to file
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(_filePath, JsonSerializer.Serialize(tasks, options));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating task {updatedTask.Id}");
                throw;
            }
        }
    }
}
