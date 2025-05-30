using FirstPartyAgent.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.IO; // Added for StreamReader and Path
using System.Linq; // Added for LINQ operations
using Newtonsoft.Json; // Added for JSON deserialization
using FirstPartyAgent.Core.Models;
using Microsoft.Azure.Cosmos; // Added for WawsAlertDetails, AlertDetails, IcmTeam

namespace Agent.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigController : Controller
    {
        private readonly IIcmAgentConfigService _icmConfigService;
        private readonly ILogger<ConfigController> _logger;
        private readonly ICosmosDBService _cosmosDbService;
        private const string IcmAgentAlertDetailsCosmosDbContainer = "IcmAlertDetails"; 

        public ConfigController(IIcmAgentConfigService icmConfigService, ILogger<ConfigController> logger, ICosmosDBService cosmosDbService)
        {
            _icmConfigService = icmConfigService ?? throw new ArgumentNullException(nameof(icmConfigService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cosmosDbService = cosmosDbService ?? throw new ArgumentNullException(nameof(cosmosDbService));
        }

        // GET api/config/containers
        [HttpGet("containers")]
        public async Task<IActionResult> ListAllContainers()
        {
            try
            {
                _logger.LogInformation("Attempting to list all containers.");
                var containers = await _icmConfigService.ListAllContainers();
                _logger.LogInformation($"Successfully retrieved {containers.Count} containers.");
                return Ok(containers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing all containers.");
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // GET api/config/containers/{containerName}/documents
        [HttpGet("containers/{containerName}/documents")]
        public async Task<IActionResult> GetAllDocumentIds(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                _logger.LogWarning("GetAllDocumentIds called with empty container name.");
                return BadRequest("Container name cannot be empty.");
            }

            try
            {
                _logger.LogInformation($"Attempting to get all document IDs for container: {containerName}.");
                var documentIds = await _icmConfigService.GetAllDocumentIds(containerName);
                _logger.LogInformation($"Successfully retrieved {documentIds.Count} document IDs for container: {containerName}.");
                return Ok(documentIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting all document IDs for container: {containerName}.");
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // GET api/config/containers/{containerName}/documents/{documentId}
        [HttpGet("containers/{containerName}/documents/{documentId}")]
        public async Task<IActionResult> GetDocumentById(string containerName, string documentId)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                _logger.LogWarning("GetDocumentById called with empty container name.");
                return BadRequest("Container name cannot be empty.");
            }
            if (string.IsNullOrWhiteSpace(documentId))
            {
                _logger.LogWarning("GetDocumentById called with empty document ID.");
                return BadRequest("Document ID cannot be empty.");
            }

            try
            {
                _logger.LogInformation($"Attempting to get document by ID: {documentId} from container: {containerName}.");
                var documentJson = await _icmConfigService.GetDocumentById(containerName, documentId);
                _logger.LogInformation($"Successfully retrieved document by ID: {documentId} from container: {containerName}.");
                return Content(documentJson, "application/json");
            }
            catch (KeyNotFoundException knfEx)
            {
                _logger.LogWarning(knfEx, $"Document with ID '{documentId}' not found in container '{containerName}'.");
                return NotFound(knfEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting document by ID: {documentId} from container: {containerName}.");
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // POST api/config/containers/{containerName}/documents
        [HttpPost("containers/{containerName}/documents")]
        public async Task<IActionResult> UpsertDocument(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                _logger.LogWarning("UpsertDocument called with empty container name.");
                return BadRequest("Container name cannot be empty.");
            }


            string documentJson = await new StreamReader(Request.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(documentJson))
            {
                _logger.LogWarning("UpsertDocument called with empty document JSON.");
                return BadRequest("Document JSON cannot be null or empty.");
            }

            try
            {
                _logger.LogInformation($"Attempting to upsert document in container: {containerName}.");
                var upsertedDocumentJson = await _icmConfigService.UpsertDocument(containerName, documentJson);
                _logger.LogInformation($"Successfully upserted document in container: {containerName}.");
                return Content(upsertedDocumentJson, "application/json");
            }
            catch (ArgumentException argEx) 
            {
                _logger.LogWarning(argEx, $"Argument error during upserting document in container: {containerName}.");
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error upserting document in container: {containerName}.");
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // POST api/config/ImportAlertDetails
        [HttpPost("ImportAlertDetails")]
        public async Task<IActionResult> ImportAlertDetails()
        {
            _logger.LogInformation("Processing ImportAlertDetails request");

            try
            {
                string fileContent = await new StreamReader(Request.Body).ReadToEndAsync();

                if (string.IsNullOrEmpty(fileContent))
                {
                    _logger.LogWarning("ImportAlertDetails called with empty file content.");
                    return BadRequest(new { error = "File content is empty" });
                }

                List<WawsAlertDetails> wawsAlertDetailsList;
                try
                {
                    wawsAlertDetailsList = JsonConvert.DeserializeObject<List<WawsAlertDetails>>(fileContent);

                    if (wawsAlertDetailsList == null || !wawsAlertDetailsList.Any())
                    {
                        _logger.LogWarning("No valid AlertDetails found in uploaded file.");
                        return BadRequest(new { error = "No valid AlertDetails found in file" });
                    }
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    _logger.LogError($"Failed to parse file content: {ex.Message}");
                    return BadRequest(new { error = $"Failed to parse file content: {ex.Message}" });
                }

                _logger.LogInformation($"Successfully parsed {wawsAlertDetailsList.Count} WawsAlertDetails from file");

                // Note: Path.Combine(AppContext.BaseDirectory, "IcmTeams.json") assumes IcmTeams.json is in the app's base execution directory.
                // For ASP.NET Core, consider using IWebHostEnvironment or configuration to manage file paths.
                var teamsJsonPath = Path.Combine(AppContext.BaseDirectory, "IcmTeams.json");
                if (!System.IO.File.Exists(teamsJsonPath))
                {
                    _logger.LogError($"IcmTeams.json not found at {teamsJsonPath}");
                    return StatusCode((int)HttpStatusCode.InternalServerError, new { error = "Configuration file IcmTeams.json not found." });
                }
                var icmTeams = JsonConvert.DeserializeObject<List<IcmTeam>>(await System.IO.File.ReadAllTextAsync(teamsJsonPath));
                var teamNameMap = icmTeams.ToDictionary(t => t.IcmTeamName.ToLowerInvariant(), t => t.IcmTeamId);

                var alertDetails = wawsAlertDetailsList
                    .Where(a => a.Actions != null && a.Actions.Any(act => !string.IsNullOrWhiteSpace(act.TeamAssignedTo)))
                    .Select(a =>
                    {
                        var alertDetail = new AlertDetails(a); // Assumes AlertDetails has a constructor accepting WawsAlertDetails
                        var action = a.Actions.FirstOrDefault(act => !string.IsNullOrWhiteSpace(act.TeamAssignedTo));
                        if (action != null) {
                            alertDetail.TeamAssignedTo = action.TeamAssignedTo;
                            if (teamNameMap.TryGetValue(action.TeamAssignedTo.ToLowerInvariant(), out var teamId))
                            {
                                alertDetail.TeamId = teamId;
                            }
                            else
                            {
                                alertDetail.TeamId = null; // Or handle as an error/default
                            }
                            alertDetail.RoutingID = action.RoutingID;
                            alertDetail.Severity = action.Severity;
                        }
                        return alertDetail;
                    })
                    .ToList();


                foreach (var group in alertDetails.GroupBy(a => a.TeamId))
                {
                    await _cosmosDbService.BulkWriteAsync(
                    _cosmosDbService.IcmAgentDatabaseName, // Or a specific database name if different
                    IcmAgentAlertDetailsCosmosDbContainer,
                    group,
                    new PartitionKey(group.Key ?? 0));
                }

                _logger.LogInformation($"Successfully imported {alertDetails.Count} alert details.");
                return Ok(new { message = $"Successfully imported {alertDetails.Count} alert details" });
            }
            catch (FileNotFoundException fnfEx)
            {
                _logger.LogError(fnfEx, "Error during ImportAlertDetails due to missing file (e.g., IcmTeams.json).");
                return StatusCode((int)HttpStatusCode.InternalServerError, new { error = $"Server configuration error: {fnfEx.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process ImportAlertDetails request.");
                return StatusCode((int)HttpStatusCode.InternalServerError, new { error = $"Failed to process request: {ex.Message}" });
            }
        }
    }
}
