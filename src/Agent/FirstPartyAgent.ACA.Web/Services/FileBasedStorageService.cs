// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.ACA.Web.Services;

public class FileBasedStorageService : ITaskStorageService
{
    private ILogger<FileBasedStorageService> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly string _filePath;

    public FileBasedStorageService(ILogger<FileBasedStorageService> logger)
    {
        _logger = logger;
        // TODO: Refactor to setting.
        _filePath =  "/mnt/task-storage/tasks.json";
    }

    public async Task SaveTaskAsync(QuotaIncidentState state)
    {
        await _semaphore.WaitAsync();
        _logger.LogInformation($"Lock acquired. Saving task {state.Incident.IncidentId} to file {_filePath}");
        try
        {
            var tasks = await GetAllTasksWithoutLockAsync();
            tasks[state.Incident.IncidentId] = state;
            var json = JsonSerializer.Serialize(tasks);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error saving task {state.Incident.IncidentId} to file {_filePath}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<Dictionary<string, QuotaIncidentState>> GetAllTasksWithoutLockAsync()
    {
        var json = await File.ReadAllTextAsync(_filePath);
        var tasks = JsonSerializer.Deserialize<Dictionary<string, QuotaIncidentState>>(json) ?? new Dictionary<string, QuotaIncidentState>();
        _logger.LogInformation($"Read {tasks.Count} tasks from file {_filePath}");

        return tasks;
    }

    private Task EnsureFileExistsAsync()
    {
        if (!File.Exists(_filePath))
        {
            return File.WriteAllTextAsync(_filePath, "{}");
        }
        return Task.CompletedTask;
    }

    public async Task<Dictionary<string, QuotaIncidentState>> GetAllTasksAsync()
    {
        await _semaphore.WaitAsync();
        await EnsureFileExistsAsync();

        _logger.LogInformation($"Lock acquired. Reading tasks from file {_filePath}");

        try
        {
            return await GetAllTasksWithoutLockAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading tasks file {_filePath}");
            return new Dictionary<string, QuotaIncidentState>();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveTaskAsync(string incidentId)
    {
        await _semaphore.WaitAsync();
        await EnsureFileExistsAsync();

        _logger.LogInformation($"Lock acquired. Removing task {incidentId} from file {_filePath}");
        try
        {
            var tasks = await GetAllTasksWithoutLockAsync();
            tasks.Remove(incidentId);
            var json = JsonSerializer.Serialize(tasks);
            await File.WriteAllTextAsync(_filePath, json);
            _logger.LogInformation($"Task {incidentId} removed from file {_filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing task {incidentId} from file {_filePath}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateTaskAsync(QuotaIncidentState state)
    {
        await _semaphore.WaitAsync();
        await EnsureFileExistsAsync();

        _logger.LogInformation($"Lock acquired. Updating task {state.Incident.IncidentId} in file {_filePath}");
        try
        {
            var tasks = await GetAllTasksWithoutLockAsync();
            tasks[state.Incident.IncidentId] = state;
            var json = JsonSerializer.Serialize(tasks);
            await File.WriteAllTextAsync(_filePath, json);
            _logger.LogInformation($"Task {state.Incident.IncidentId} updated in file {_filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating task {state.Incident.IncidentId} in file {_filePath}");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}