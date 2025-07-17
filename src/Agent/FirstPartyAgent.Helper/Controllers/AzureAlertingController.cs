using System.Net;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Models;
using Azure.Identity;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helper.Models;
using FirstPartyAgent.Helper.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Configuration;
using Microsoft.Graph.Models;
using Newtonsoft.Json;
using ReverseMarkdown;

namespace FirstPartyAgent.Helper.Controllers;


[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AzureAlertingController : ControllerBase
{
    private ILogger<AzureAlertingController> _logger;
    private IStorageService _storeageService;
    private ICosmosDBService _cosmosDBService;
    private StorageAccountSettings _storageAccountSettings;
    private const string _azureAlertingDetailsFileName = "azure_alerting_details.json";
    private const string _icmTeamsFileName = "IcmTeamsMap.json";
    private const string _alertDetailsCosmosDbContainer = "IcmAlertDetails";

    public AzureAlertingController(
        ILogger<AzureAlertingController> logger,
        IStorageService storeageService,
        StorageAccountSettings storageAccountSettings,
        ICosmosDBService cosmosDBService,
        IConfiguration config)
    {
        _logger = logger;
        _storeageService = storeageService;
        _cosmosDBService = cosmosDBService;
        _storageAccountSettings = storageAccountSettings;

    }

    [HttpGet("AzureAlertingDetails")]
    public async Task<IActionResult> AzureAlertingDetails()
    {
        // read the file stream and return it as stream
        var stream = await _storeageService.ReadFileStreamFromStorage(_storageAccountSettings.SreAgentHelperContainerName, _azureAlertingDetailsFileName);

        if (stream == null)
        {
            _logger.LogError("AzureAlertingDetails file not found in storage.");
            return NotFound("AzureAlertingDetails file not found.");
        }

        // return the stream as file result
        return new FileStreamResult(stream, "application/json")
        {
            FileDownloadName = _azureAlertingDetailsFileName
        };
    }

    [HttpPost("Import")]
    public async Task<IActionResult> ImportAlertingDetails()
    {
        // read file stream from body
        string fileContent = await new StreamReader(Request.Body).ReadToEndAsync();

        if (string.IsNullOrEmpty(fileContent))
        {
            return BadRequest("File content is empty or null.");
        }

        // convert the stream to json without reading the whole file into memory
        var wawsAlertDetailsList = System.Text.Json.JsonSerializer.Deserialize<List<WawsAlertDetails>>(fileContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });


        if (wawsAlertDetailsList == null || !wawsAlertDetailsList.Any())
        {
            return BadRequest("No valid alerting details found in the uploaded file.");
        }

        string icmTeamsJson = await _storeageService.ReadFileFromStorage(_storageAccountSettings.SreAgentHelperContainerName, _icmTeamsFileName);
        var serviceTeamMaps = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(icmTeamsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (serviceTeamMaps == null || !serviceTeamMaps.Any())
        {
            _logger.LogError("IcmTeamsMap file not found or empty in storage.");
            return NotFound("IcmTeamsMap file not found or empty.");
        }

        var alertDetails = wawsAlertDetailsList
            .Where(a => a.Actions?.Any(act => !string.IsNullOrWhiteSpace(act.TeamAssignedTo)) == true)
            .Where(a => serviceTeamMaps.ContainsKey(a.ServiceId))
            .Select(a =>
            {
                var teamNameMap = serviceTeamMaps[a.ServiceId];
                var alertDetail = new AlertDetails(a);
                var action = a.Actions.FirstOrDefault(act => !string.IsNullOrWhiteSpace(act.TeamAssignedTo));
                if (action != null)
                {
                    alertDetail.TeamAssignedTo = action.TeamAssignedTo;
                    alertDetail.TeamId = teamNameMap.TryGetValue(action.TeamAssignedTo.ToLower(), out var teamId) ? teamId : null;
                    alertDetail.RoutingID = action.RoutingID;
                    alertDetail.Severity = action.Severity;
                }
                return alertDetail;
            }).ToList();

        foreach (var group in alertDetails.GroupBy(a => a.TeamId))
        {
            await _cosmosDBService.BulkWriteAsync(
                _cosmosDBService.IcmAgentDatabaseName,
                _alertDetailsCosmosDbContainer,
                group,
                new Microsoft.Azure.Cosmos.PartitionKey(group.Key ?? 0));
        }

        return Ok($"Successfully imported {alertDetails.Count()} alert details");
    }

    [HttpGet("GetByTeamId/{teamId}")]
    public async Task<IActionResult> GetAlertDetails(int? teamId)
    {
        if (teamId == null)
        {
            return BadRequest("Team ID cannot be null or empty.");
        }
        try
        {
            var alertDetails = await _cosmosDBService.GetQueryableContainer<AlertDetails>(_cosmosDBService.IcmAgentDatabaseName, _alertDetailsCosmosDbContainer)
                .Where(ad => ad.TeamId == teamId)
                .ToListAsync();
            if (alertDetails == null || !alertDetails.Any())
            {
                return NotFound($"No alert details found for team ID: {teamId}");
            }
            return Ok(alertDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching alert details for team ID: {TeamId}", teamId);
            return StatusCode(500, "Internal server error while fetching alert details.");
        }
    }
}
