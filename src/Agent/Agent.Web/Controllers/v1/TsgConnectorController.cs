// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using Agent.Core.Helpers;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Web.Authorization;
using Agent.Web.Models.Connectors;
using Agent.Web.Services;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1;

/// <summary>
/// Controller for TSG connector dataplane operations (Azure DevOps and GitHub)
/// Supports PAT-based authentication for cross-tenant scenarios
/// </summary>
[ApiController]
[Route("api/v1/connectors/tsgcrawler")]
public class TsgConnectorController : ControllerBase
{
    private readonly ITsgConnectorRepository _repository;
    private readonly ILogger<TsgConnectorController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TsgConnectorCloneService _cloneService;

    public TsgConnectorController(
        ITsgConnectorRepository repository,
        ILogger<TsgConnectorController> logger,
        IHttpClientFactory httpClientFactory,
        TsgConnectorCloneService cloneService)
    {
        _repository = repository;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cloneService = cloneService;
    }

    /// <summary>
    /// Convert a document to a response DTO
    /// </summary>
    private static TsgConnectorResponse ToResponse(TsgConnectorDocument document)
    {
        return new TsgConnectorResponse
        {
            Name = document.Name,
            DataSource = document.DataSource,
            RepoType = document.RepoType,
            HasCredentials = !string.IsNullOrEmpty(document.Pat),
            Status = document.Status,
            LastValidated = document.LastValidated,
            ErrorMessage = document.ErrorMessage,
            CloneStatus = document.CloneStatus,
            LastSuccessfulSync = document.LastSuccessfulSync,
            LocalPath = document.LocalPath,
            LatestCommit = document.LatestCommit
        };
    }

    /// <summary>
    /// Create a document from a request DTO
    /// </summary>
    private static TsgConnectorDocument FromRequest(TsgConnectorRequest request)
    {
        return new TsgConnectorDocument
        {
            Id = TsgConnectorDocument.GetId(request.Name),
            Name = request.Name,
            DataSource = request.DataSource,
            RepoType = RepoTypeHelper.DetectRepoType(request.DataSource),
            Pat = request.PersonalAccessToken,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = ConnectorStatus.Healthy
        };
    }

    /// <summary>
    /// Create or update a TSG connector with PAT authentication
    /// </summary>
    [HttpPost]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [ProducesResponseType(typeof(TsgConnectorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TsgConnectorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrUpdateConnector([FromBody] TsgConnectorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Connector name is required" });
        }

        if (string.IsNullOrWhiteSpace(request.DataSource))
        {
            return BadRequest(new { error = "DataSource (repository URL) is required" });
        }

        if (string.IsNullOrWhiteSpace(request.PersonalAccessToken))
        {
            return BadRequest(new { error = "Personal Access Token is required" });
        }

        try
        {
            _logger.LogInternalInformation("Creating/updating TSG connector: {ConnectorName}",
                request.Name);

            // Check if connector already exists
            var existingConnector = await _repository.GetByNameAsync(request.Name);

            // Reject DataSource changes (user must delete & recreate)
            if (existingConnector != null
                && request.DataSource != existingConnector.DataSource)
            {
                return BadRequest(new
                {
                    error = "DataSource cannot be modified after creation. To change the repository URL, delete this connector and create a new one."
                });
            }

            // For updates, modify existing document; for creates, build new document
            TsgConnectorDocument document;
            if (existingConnector != null)
            {
                document = existingConnector;
                // Check if PAT changed and trigger credential update if needed
                if (document.Pat != request.PersonalAccessToken)
                {
                    _logger.LogInternalInformation("PAT update requested for TSG connector: {request.Name}");
                    document.CloneStatus = CloneStatus.PendingCredentialUpdate;
                    document.Pat = request.PersonalAccessToken;
                }
                document.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                document = FromRequest(request);
            }

            // Test connectivity before saving (dispatch based on repo type)
            _logger.LogInternalInformation("Testing connectivity for TSG connector: {ConnectorName} (RepoType: {RepoType})",
                request.Name, document.RepoType);
            var patToTest = document.Pat;
            var testResult = document.RepoType == RepoType.GitHub
                ? await TestGitHubConnectivity(document.DataSource, patToTest)
                : await TestAzureDevOpsConnectivity(document.DataSource, patToTest);

            if (!testResult.IsSuccessful)
            {
                _logger.LogInternalWarning("Connectivity test failed for TSG connector: {ConnectorName} - {Error}",
                    request.Name, testResult.ErrorMessage);
                return BadRequest(new
                {
                    error = "Connectivity test failed",
                    details = testResult.ErrorMessage
                });
            }

            // Set status to Healthy since connectivity test passed
            document.Status = ConnectorStatus.Healthy;
            document.LastValidated = DateTime.UtcNow;
            document.ErrorMessage = null;

            // Save to repository (Cosmos DB handles encryption at rest)
            var savedDocument = await _repository.UpsertAsync(document);

            // Queue background clone/sync operation
            _cloneService.QueueCodeRepositoryUpdate();
            _logger.LogInternalInformation("Queued code repository update for TSG connector: {ConnectorName}", request.Name);

            var response = ToResponse(savedDocument);

            return existingConnector != null
                ? Ok(response)
                : CreatedAtAction(nameof(GetConnector), new { name = request.Name }, response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to create/update TSG connector: {ConnectorName}", request.Name);
            return StatusCode(500, new { error = "Failed to create/update connector", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a TSG connector by name (PAT is masked)
    /// </summary>
    [HttpGet("{name}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(TsgConnectorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConnector(string name)
    {
        try
        {
            var connector = await _repository.GetByNameAsync(name);
            if (connector == null)
            {
                return NotFound(new { error = $"Connector '{name}' not found" });
            }

            return Ok(ToResponse(connector));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to get TSG connector: {ConnectorName}", name);
            return StatusCode(500, new { error = "Failed to get connector", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all TSG connectors
    /// </summary>
    [HttpGet]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(IEnumerable<TsgConnectorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllConnectors()
    {
        try
        {
            var connectors = await _repository.GetAllAsync();
            var responses = connectors.Select(ToResponse);
            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to get all TSG connectors");
            return StatusCode(500, new { error = "Failed to get connectors", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete a TSG connector and its PAT
    /// </summary>
    [HttpDelete("{name}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConnector(string name)
    {
        try
        {
            _logger.LogInternalInformation("Deleting TSG connector: {ConnectorName}", name);

            var deleted = await _repository.DeleteAsync(name);
            if (!deleted)
            {
                return NotFound(new { error = $"Connector '{name}' not found" });
            }

            _cloneService.DeleteLocalRepository(name);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to delete TSG connector: {ConnectorName}", name);
            return StatusCode(500, new { error = "Failed to delete connector", details = ex.Message });
        }
    }

    /// <summary>
    /// Test connectivity with the Azure DevOps repository
    /// </summary>
    [HttpPost("{name}/test")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(TsgConnectorTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnectivity(string name)
    {
        try
        {
            var connector = await _repository.GetByNameAsync(name);
            if (connector == null)
            {
                return NotFound(new { error = $"Connector '{name}' not found" });
            }

            _logger.LogInternalInformation("Testing connectivity for TSG connector: {ConnectorName}", name);

            // Get the PAT for testing
            var pat = await _repository.GetPatAsync(name);
            if (string.IsNullOrEmpty(pat))
            {
                await _repository.UpdateStatusAsync(name, ConnectorStatus.Unhealthy, "No PAT configured");
                return Ok(new TsgConnectorTestResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = "No Personal Access Token configured for this connector"
                });
            }

            // Test connectivity (dispatch based on repo type)
            var testResult = connector.RepoType == RepoType.GitHub
                ? await TestGitHubConnectivity(connector.DataSource, pat)
                : await TestAzureDevOpsConnectivity(connector.DataSource, pat);

            // Update connector status
            await _repository.UpdateStatusAsync(
                name,
                testResult.IsSuccessful ? ConnectorStatus.Healthy : ConnectorStatus.Unhealthy,
                testResult.ErrorMessage);

            return Ok(testResult);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to test connectivity for TSG connector: {ConnectorName}", name);
            await _repository.UpdateStatusAsync(name, ConnectorStatus.Unhealthy, ex.Message);

            return Ok(new TsgConnectorTestResponse
            {
                IsSuccessful = false,
                ErrorMessage = $"Test failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Trigger a sync (git pull) for a connector's repository
    /// </summary>
    [HttpPost("{name}/sync")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncRepository(string name)
    {
        try
        {
            var connector = await _repository.GetByNameAsync(name);
            if (connector == null)
            {
                return NotFound(new { error = $"Connector '{name}' not found" });
            }

            _logger.LogInternalInformation("Triggering sync for TSG connector: {ConnectorName}", name);

            // Queue the sync operation
            _cloneService.QueueCodeRepositoryUpdate();

            return Accepted(new
            {
                message = "Sync operation queued",
                connectorName = name,
                currentCloneStatus = connector.CloneStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to trigger sync for TSG connector: {ConnectorName}", name);
            return StatusCode(500, new { error = "Failed to trigger sync", details = ex.Message });
        }
    }

    /// <summary>
    /// Test connectivity to Azure DevOps by fetching repository info
    /// </summary>
    private async Task<TsgConnectorTestResponse> TestAzureDevOpsConnectivity(string dataSource, string? pat)
    {
        try
        {
            // Parse the Azure DevOps URL to extract org, project, and repo
            var uri = new Uri(dataSource);

            // Expected format: https://dev.azure.com/{org}/{project}/_git/{repo}
            // or: https://{org}.visualstudio.com/{project}/_git/{repo}
            string apiUrl;

            if (uri.Host.Contains("visualstudio.com"))
            {
                // Legacy format: https://{org}.visualstudio.com/{project}/_git/{repo}
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 3)
                {
                    return new TsgConnectorTestResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = "Invalid Azure DevOps URL format"
                    };
                }

                var project = segments[0];
                apiUrl = $"https://{uri.Host}/{project}/_apis/git/repositories?api-version=7.0";
            }
            else if (uri.Host.Contains("dev.azure.com"))
            {
                // New format: https://dev.azure.com/{org}/{project}/_git/{repo}
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2)
                {
                    return new TsgConnectorTestResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = "Invalid Azure DevOps URL format"
                    };
                }

                var org = segments[0];
                var project = segments[1];
                apiUrl = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories?api-version=7.0";
            }
            else
            {
                return new TsgConnectorTestResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = "Unsupported Azure DevOps URL format. Expected dev.azure.com or visualstudio.com"
                };
            }

            // Make the API request
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrEmpty(pat))
            {
                // Use PAT authentication
                var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            var response = await httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                return new TsgConnectorTestResponse
                {
                    IsSuccessful = true,
                    Details = "Successfully connected to Azure DevOps repository"
                };
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                return new TsgConnectorTestResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = $"Azure DevOps API returned {response.StatusCode}: {content}"
                };
            }
        }
        catch (Exception ex)
        {
            return new TsgConnectorTestResponse
            {
                IsSuccessful = false,
                ErrorMessage = $"Connection test failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Test connectivity to GitHub by fetching repository info
    /// </summary>
    private async Task<TsgConnectorTestResponse> TestGitHubConnectivity(string dataSource, string? pat)
    {
        try
        {
            // Parse owner/repo from URL
            var uri = new Uri(dataSource);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length < 2)
            {
                return new TsgConnectorTestResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = "Invalid GitHub URL format. Expected: https://github.com/{owner}/{repo}"
                };
            }

            var owner = segments[0];
            var repo = segments[1].TrimEnd(".git".ToCharArray());

            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SREAgent/1.0");

            if (!string.IsNullOrEmpty(pat))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
            }

            var response = await httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                return new TsgConnectorTestResponse
                {
                    IsSuccessful = true,
                    Details = "Successfully connected to GitHub repository"
                };
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                return new TsgConnectorTestResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = $"GitHub API returned {response.StatusCode}: {content}"
                };
            }
        }
        catch (Exception ex)
        {
            return new TsgConnectorTestResponse
            {
                IsSuccessful = false,
                ErrorMessage = $"Connection test failed: {ex.Message}"
            };
        }
    }
}
