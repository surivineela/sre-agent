using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstPartyAgent.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace FirstPartyAgent.Core.Services;

/// <summary>
/// Service for managing emerging issues configurations with CosmosDB or local file storage
/// </summary>
public class EmergingIssueConfigService : IEmergingIssueConfigService
{
    private readonly ICosmosDBService _cosmosDbService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmergingIssueConfigService> _logger;

    private const string _databaseName = "HotsiteAgent";
    private const string _emergingIssuesContainerName = "EmergingIssues";
    private readonly string _localStoragePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmergingIssueConfigService"/> class
    /// </summary>
    public EmergingIssueConfigService(
        IWebHostEnvironment env, 
        ICosmosDBService cosmosDbService,
        ILogger<EmergingIssueConfigService> logger)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _cosmosDbService = cosmosDbService ?? throw new ArgumentNullException(nameof(cosmosDbService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Set up local storage path for fallback
        _localStoragePath = Path.Combine(_env.ContentRootPath, "Data", "EmergingIssues");
        
        // Ensure directory exists if we ever need to use local storage
        if (!Directory.Exists(_localStoragePath))
        {
            Directory.CreateDirectory(_localStoragePath);
        }
    }

    private bool IsCosmosDbEnabled() => _cosmosDbService.IsEnabled && _cosmosDbService.CosmosClient != null;

    /// <inheritdoc/>
    public bool IsEnabled() => true;

    /// <inheritdoc/>
    public async Task<string> RegisterEmergingIssue(EmergingIssueConfig emergingIssue)
    {
        if (emergingIssue == null)
        {
            throw new ArgumentNullException(nameof(emergingIssue), "Emerging issue cannot be null");
        }

        if (string.IsNullOrWhiteSpace(emergingIssue.IncidentId))
        {
            throw new ArgumentException("Incident ID cannot be empty", nameof(emergingIssue));
        }

        if (string.IsNullOrWhiteSpace(emergingIssue.OwningTeam))
        {
            throw new ArgumentException("Owning team cannot be empty", nameof(emergingIssue));
        }

        try
        {
            // Check if it already exists
            try
            {
                var existingIssue = await GetEmergingIssue(emergingIssue.IncidentId);
                throw new InvalidOperationException($"Emerging issue with incident ID {emergingIssue.IncidentId} already exists");
            }
            catch (KeyNotFoundException)
            {
                // This is expected if the issue doesn't exist yet
            }

            // Set up the metadata
            emergingIssue.Id = Guid.NewGuid().ToString();
            emergingIssue.CreatedDate = DateTime.UtcNow;
            emergingIssue.LastModifiedDate = DateTime.UtcNow;

            if (IsCosmosDbEnabled())
            {
                // Store in CosmosDB
                await _cosmosDbService.UpsertItemAsync(
                    _cosmosDbService.IcmAgentDatabaseName,
                    _emergingIssuesContainerName, 
                    emergingIssue);
                
                _logger.LogInformation("Registered emerging issue with ID {Id} in CosmosDB", emergingIssue.Id);
            }
            else
            {
                // Fallback to local storage
                string filePath = Path.Combine(_localStoragePath, $"{emergingIssue.Id}.json");
                await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(emergingIssue, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
                
                _logger.LogInformation("Registered emerging issue with ID {Id} in local storage", emergingIssue.Id);
            }

            return emergingIssue.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering emerging issue: {Message}", ex.Message);
            throw new Exception($"Error registering emerging issue: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateEmergingIssue(EmergingIssueConfig emergingIssue)
    {
        if (emergingIssue == null)
        {
            throw new ArgumentNullException(nameof(emergingIssue), "Emerging issue cannot be null");
        }

        if (string.IsNullOrWhiteSpace(emergingIssue.IncidentId))
        {
            throw new ArgumentException("Incident ID cannot be empty", nameof(emergingIssue));
        }

        try
        {
            // Get the existing issue to ensure it exists and to preserve the original ID
            var existingIssue = await GetEmergingIssue(emergingIssue.IncidentId);
            
            // Update metadata
            emergingIssue.Id = existingIssue.Id;
            emergingIssue.CreatedDate = existingIssue.CreatedDate;
            emergingIssue.LastModifiedDate = DateTime.UtcNow;
            
            if (IsCosmosDbEnabled())
            {
                // Update in CosmosDB
                await _cosmosDbService.UpsertItemAsync(
                    _cosmosDbService.IcmAgentDatabaseName,
                    _emergingIssuesContainerName, 
                    emergingIssue);
                
                _logger.LogInformation("Updated emerging issue with ID {Id} in CosmosDB", emergingIssue.Id);
            }
            else
            {
                // Fallback to local storage
                string filePath = Path.Combine(_localStoragePath, $"{emergingIssue.Id}.json");
                await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(emergingIssue, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
                
                _logger.LogInformation("Updated emerging issue with ID {Id} in local storage", emergingIssue.Id);
            }
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Emerging issue with incident ID {IncidentId} not found", emergingIssue.IncidentId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating emerging issue: {Message}", ex.Message);
            throw new Exception($"Error updating emerging issue: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task DeregisterEmergingIssue(string incidentId)
    {
        if (string.IsNullOrWhiteSpace(incidentId))
        {
            throw new ArgumentException("Incident ID cannot be empty", nameof(incidentId));
        }

        try
        {
            // Get the existing issue to use its ID and team
            var existingIssue = await GetEmergingIssue(incidentId);
            
            if (IsCosmosDbEnabled())
            {
                // Delete from CosmosDB
                var container = _cosmosDbService.CosmosClient.GetContainer(
                    _cosmosDbService.IcmAgentDatabaseName, 
                    _emergingIssuesContainerName);
                
                await container.DeleteItemAsync<EmergingIssueConfig>(
                    existingIssue.Id, 
                    new PartitionKey(existingIssue.OwningTeam));
                
                _logger.LogInformation("Deregistered emerging issue with ID {Id} from CosmosDB", existingIssue.Id);
            }
            else
            {
                // Fallback to local storage
                string filePath = Path.Combine(_localStoragePath, $"{existingIssue.Id}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deregistered emerging issue with ID {Id} from local storage", existingIssue.Id);
                }
                else
                {
                    _logger.LogWarning("File for emerging issue with ID {Id} not found in local storage", existingIssue.Id);
                    throw new KeyNotFoundException($"Emerging issue with ID {existingIssue.Id} not found");
                }
            }
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Emerging issue with incident ID {IncidentId} not found", incidentId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deregistering emerging issue: {Message}", ex.Message);
            throw new Exception($"Error deregistering emerging issue: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<EmergingIssueConfig> GetEmergingIssue(string incidentId)
    {
        if (string.IsNullOrWhiteSpace(incidentId))
        {
            throw new ArgumentException("Incident ID cannot be empty", nameof(incidentId));
        }

        try
        {
            if (IsCosmosDbEnabled())
            {
                // Query CosmosDB
                var queryableResult = _cosmosDbService.GetQueryableContainer<EmergingIssueConfig>(
                    _cosmosDbService.IcmAgentDatabaseName, 
                    _emergingIssuesContainerName)
                    .Where(e => e.IncidentId == incidentId)
                    .ToList();

                if (queryableResult == null || !queryableResult.Any())
                {
                    _logger.LogWarning("Emerging issue with incident ID {IncidentId} not found in CosmosDB", incidentId);
                    throw new KeyNotFoundException($"Emerging issue with incident ID {incidentId} not found");
                }

                return queryableResult.First();
            }
            else
            {
                // Fallback to local storage
                // Since we don't have an index in the local storage, we need to load all files and filter
                var allIssues = await LoadAllLocalEmergingIssues();
                var issue = allIssues.FirstOrDefault(e => e.IncidentId == incidentId);

                if (issue == null)
                {
                    _logger.LogWarning("Emerging issue with incident ID {IncidentId} not found in local storage", incidentId);
                    throw new KeyNotFoundException($"Emerging issue with incident ID {incidentId} not found");
                }

                return issue;
            }
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting emerging issue: {Message}", ex.Message);
            throw new Exception($"Error getting emerging issue: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<List<EmergingIssueConfig>> ListEmergingIssues()
    {
        try
        {
            if (IsCosmosDbEnabled())
            {
                // Query CosmosDB for all emerging issues
                var queryableResult = _cosmosDbService.GetQueryableContainer<EmergingIssueConfig>(
                    _cosmosDbService.IcmAgentDatabaseName, 
                    _emergingIssuesContainerName)
                    .ToList();

                return queryableResult ?? new List<EmergingIssueConfig>();
            }
            else
            {
                // Fallback to local storage
                return await LoadAllLocalEmergingIssues();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing emerging issues: {Message}", ex.Message);
            throw new Exception($"Error listing emerging issues: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<List<EmergingIssueConfig>> ListEmergingIssuesByTeam(string owningTeam)
    {
        if (string.IsNullOrWhiteSpace(owningTeam))
        {
            throw new ArgumentException("Owning team cannot be empty", nameof(owningTeam));
        }

        try
        {
            if (IsCosmosDbEnabled())
            {
                // Query CosmosDB for emerging issues by team
                var queryableResult = _cosmosDbService.GetQueryableContainer<EmergingIssueConfig>(
                    _cosmosDbService.IcmAgentDatabaseName, 
                    _emergingIssuesContainerName)
                    .Where(e => e.OwningTeam == owningTeam)
                    .ToList();

                return queryableResult ?? new List<EmergingIssueConfig>();
            }
            else
            {
                // Fallback to local storage - load all and filter by team
                var allIssues = await LoadAllLocalEmergingIssues();
                return allIssues.Where(e => e.OwningTeam == owningTeam).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing emerging issues by team: {Message}", ex.Message);
            throw new Exception($"Error listing emerging issues by team: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads all emerging issues from local storage
    /// </summary>
    private async Task<List<EmergingIssueConfig>> LoadAllLocalEmergingIssues()
    {
        var issues = new List<EmergingIssueConfig>();
        var directory = new DirectoryInfo(_localStoragePath);
        
        if (!directory.Exists)
        {
            return issues;
        }

        var files = directory.GetFiles("*.json");
        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file.FullName);
                var issue = JsonSerializer.Deserialize<EmergingIssueConfig>(content);
                if (issue != null)
                {
                    issues.Add(issue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading emerging issue from file {FileName}: {Message}", file.Name, ex.Message);
                // Skip files that cannot be deserialized
            }
        }

        return issues;
    }
}
