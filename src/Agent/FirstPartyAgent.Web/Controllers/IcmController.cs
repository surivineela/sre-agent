using System.Text;
using System.Text.Json;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using static FirstPartyAgent.Core.Services.ICMAgentInstructionGenerationService;

namespace Agent.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class IcmController : Controller
{
    private readonly ILogger<ApiController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IIcmAgentConfigService _icmConfigService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IICMWorkflowClient _icmWorkflowClient;
    private readonly IAlertProcessingService _alertProcessingService;
    private readonly ISessionMessageService _sessionMessageService;
    private readonly ICMAgentInstructionGenerationService _instructionGenerationService;
    private readonly TsgFetcherService _tsgFetcherService;
    public IcmController(
        ILogger<ApiController> logger,
        IConfiguration configuration,
        IIcmAgentConfigService icmConfigService,
        IHttpClientFactory httpClientFactory,
        IICMWorkflowClient icmWorkflowClient,
        IAlertProcessingService alertProcessingService,
        ISessionMessageService sessionMessageService,
        ICMAgentInstructionGenerationService instructionGenerationService
        )
    {
        _logger = logger;
        _configuration = configuration;
        _icmConfigService = icmConfigService;
        _httpClientFactory = httpClientFactory;
        _icmWorkflowClient = icmWorkflowClient;
        _alertProcessingService = alertProcessingService;
        _sessionMessageService = sessionMessageService;
        _instructionGenerationService = instructionGenerationService;
    }

    [HttpGet("isFeatureEnabled")]
    [HttpOptions("isFeatureEnabled")]
    public IActionResult IsFeatureEnabled()
    {
        return Ok(_icmConfigService.IsEnabled());
    }

    [HttpGet("getOnboardedLoops")]
    [HttpOptions("getOnboardedLoops")]
    public async Task<IActionResult> GetOnboardedLoops()
    {
        try
        {
            var results = await _icmConfigService.GetOnboardedLoops();
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving loops: {ex.Message}");
        }
    }

    [HttpGet("getLoopAlertConfigs/{loopId?}")]
    [HttpOptions("getLoopAlertConfigs/{loopId?}")]
    public async Task<IActionResult> GetLoopAlertConfigs(int? loopId)
    {
        if (loopId != null && loopId <= 0)
        {
            return BadRequest("ID must be greater than 0");
        }

        try
        {
            var result = await _icmConfigService.GetLoopAlertConfigs(loopId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving alert configs: {ex.Message}");
        }
    }


    [HttpGet("getLoopAlerts/{loopId}")]
    [HttpOptions("getLoopAlerts/{loopId}")]
    public async Task<IActionResult> GetLoopAlerts(int loopId)
    {
        if (loopId <= 0)
        {
            return BadRequest("ID must be greater than 0");
        }

        try
        {
            var result = await _icmConfigService.GetLoopAlerts(loopId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving alert configs: {ex.Message}");
        }
    }

    /// Get ICM teams
    [HttpGet("icmTeams")]
    [HttpOptions("icmTeams")]
    public async Task<IActionResult> GetICMTeams()
    {
        try
        {
            var teams = await _icmConfigService.GetIcmTeams();

            return Ok(teams);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving ICM teams: {ex.Message}");
        }
    }

    [HttpGet("agentFactoryConfig/{configName}")]
    [HttpOptions("agentFactoryConfig/{configName}")]
    public async Task<IActionResult> GetAgentFactoryConfig(string configName)
    {
        if (string.IsNullOrWhiteSpace(configName))
        {
            return BadRequest("Configuration name cannot be empty");
        }
        try
        {
            var config = await _icmConfigService.GetAgentFactoryConfig<JsonElement>(configName);
            var jsonString = System.Text.Json.JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

            return Content(jsonString, "application/json");
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Agent Factory configuration with name {configName} not found.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving Agent Factory configuration: {ex.Message}");
        }
    }

    [HttpGet("agentFactoryConfigs")]
    [HttpOptions("agentFactoryConfigs")]
    public async Task<IActionResult> GetAgentFactoryConfigs()
    {
        try
        {
            var configs = await _icmConfigService.GetAgentFactoryConfigNames();
            return Ok(configs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving Agent Factory configurations: {ex.Message}");
        }
    }

    [HttpPost("agentFactoryConfig")]
    [HttpOptions("agentFactoryConfig")]
    public async Task<IActionResult> UpsertAgentFactoryConfig()
    {
        var content = await new StreamReader(Request.Body).ReadToEndAsync();
        var config = System.Text.Json.JsonSerializer.Deserialize<AgentFactoryConfigCosmos<JsonElement>>(content);

        if (config == null)
        {
            return BadRequest("Agent Factory configuration cannot be empty");
        }
        try
        {
            await _icmConfigService.UpsertAgentFactoryConfig(config);
            return Ok(new { success = true, message = "Configuration saved successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating Agent Factory config: {ex.Message}");
        }
    }

    [HttpGet("alerts")]
    [HttpOptions("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        try
        {
            var alerts = await _icmConfigService.GetAlerts();

            var teamList = new HashSet<string> { "Windows Azure Websites Servicing", "Antares Billing Loop", "Antares FALCON Team (FrontEnd/DataRole/GRS)",
                    "Internal Only Diag", "App Service Control Plane Loop", "Antares Management Loop", "Antares Functions Loop", "App Service Sev 2 Triage", "Core Platform", "Dev Box Customizations", "Automation", "PGEscalation", "App"};

            var filtered = alerts.Where(a => !string.IsNullOrWhiteSpace(a.TeamAssignedTo) && teamList.Contains(a.TeamAssignedTo)).ToList();

            return Ok(filtered);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving Alerts.json content: {ex.Message}");
        }
    }


    [HttpGet("getAlertConfig/{loopId}/{alertId}")]
    [HttpOptions("getAlertConfig/{loopId}/{alertId}")]
    public async Task<IActionResult> GetAlertConfig(int loopId, string alertId)
    {
        if (loopId < 0)
        {
            return BadRequest("ID must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(alertId))
        {
            return BadRequest("alertId cannot be empty");
        }

        try
        {
            try
            {
                var config = await _icmConfigService.GetAlertConfig(loopId, alertId);
                return Ok(config);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Alert configuration with ID {alertId} not found.");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving alert config: {ex.Message}");
        }
    }

    [HttpPost("createAlertConfig")]
    [HttpOptions("createAlertConfig")]
    public async Task<IActionResult> CreateAlertConfig([FromBody] ICMAlertConfig alertConfig)
    {
        if (alertConfig == null)
        {
            return BadRequest("Alert configuration cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(alertConfig?.AlertingId))
        {
            return BadRequest("AlertingId cannot be empty");
        }

        try
        {
            var alertId = await _icmConfigService.CreateAlertConfig(alertConfig);
            return Ok(new { success = true, alertId = alertId, message = "Alert configuration created successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating alert config: {ex.Message}");
        }
    }

    [HttpPost("updateAlertConfig/{loopId}/{alertId}")]
    [HttpOptions("updateAlertConfig/{loopId}/{alertId}")]
    public async Task<IActionResult> UpdateAlertConfig([FromBody] ICMAlertConfig alertConfig, int loopId, string alertId)
    {
        if (alertConfig == null)
        {
            return BadRequest("Alert configuration cannot be empty");
        }
        if (string.IsNullOrWhiteSpace(alertConfig?.AlertingId))
        {
            return BadRequest("AlertingId cannot be empty");
        }
        if (loopId < 0)
        {
            return BadRequest("ID must be greater than 0");
        }
        if (string.IsNullOrWhiteSpace(alertId))
        {
            return BadRequest("alertId cannot be empty");
        }

        try
        {
            await _icmConfigService.UpdateAlertConfig(alertConfig, loopId, alertId);
            return Ok(new { success = true, message = "Configuration saved successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating alert config: {ex.Message}");
        }
    }

    [HttpGet("getIncidents")]
    [HttpOptions("getIncidents")]
    public async Task<IActionResult> GetIncidents(int loopId, int numOfDays, string title)
    {
        if (loopId < 0)
        {
            return BadRequest("ID must be greater than 0");
        }
        if (numOfDays < 0)
        {
            return BadRequest("numOfDays must be greater than or equal to 0");
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest("title cannot be empty");
        }
        try
        {
            var incidents = await _icmConfigService.GetIncidentsByTeamAlert(loopId, numOfDays, title);
            return Ok(incidents);
        }
        catch (KeyNotFoundException)
        {
            return Ok(new List<IcmIncidentBasicInfo>());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving incidents: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a list of agent deployments for a specific team.
    /// </summary>
    /// <param name="loopId">The team ID (loop ID)</param>
    /// <returns>List of agent deployments for the specified team</returns>
    [HttpGet("getAgentDeployments/{loopId}")]
    [HttpOptions("getAgentDeployments/{loopId}")]
    public async Task<IActionResult> GetAgentDeployments(int loopId)
    {
        if (loopId < 0)
        {
            return BadRequest("ID must be greater than 0");
        }
        try
        {
            var deployments = await _icmConfigService.GetAgentDeployments(loopId);
            return Ok(deployments);
        }
        catch (KeyNotFoundException)
        {
            return Ok(new List<AgentDeployment>());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving agent deployments: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the Geneva configuration containing available actions and their parameters.
    /// </summary>
    /// <returns>A JSON object containing Geneva action configurations</returns>
    [HttpGet("getGenevaConfig")]
    [HttpOptions("getGenevaConfig")]
    public async Task<IActionResult> GetGenevaConfig(int loopId)
    {
        if (loopId < 0)
        {
            return BadRequest("ID must be greater than 0");
        }
        try
        {
            var genevaConfig = await _icmConfigService.GetGenevaActionConfig(loopId);

            return Ok(genevaConfig);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Geneva Action configuration for loop ID {loopId} not found.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving Geneva Action configuration: {ex.Message}");
        }
    }

    [HttpPost("saveGenevaConfig")]
    [HttpOptions("saveGenevaConfig")]
    public async Task<IActionResult> SaveGenevaConfig([FromBody] GenevaActionsConfigCosmos genevaActionsConfig)
    {
        if (genevaActionsConfig == null)
        {
            return BadRequest("Geneva action configuration cannot be empty");
        }

        if (genevaActionsConfig.TeamId <= 0)
        {
            return BadRequest("TeamId must be greater than 0");
        }

        try
        {
            var genevaActionsConfigResult = await _icmConfigService.SaveGenevaActionsConfig(genevaActionsConfig);
            return Ok(new { success = true, genevaActionsConfig = genevaActionsConfigResult, message = "Geneva Action configuration saved successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating alert config: {ex.Message}");
        }
    }


    [HttpGet("loops")]
    [HttpOptions("loops")]
    public IActionResult GetLoops()
    {
        try
        {
            var loopsJsonPath = Path.Combine(AppContext.BaseDirectory, "IcmMetadata", "Loops.json");
            var loopsJsonContent = System.IO.File.ReadAllText(loopsJsonPath);

            return Content(loopsJsonContent, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving Loops.json content: {ex.Message}");
        }
    }



    /// <summary>
    /// Processes alert stream data by connecting to an external API and streaming the response back.
    /// </summary>
    /// <param name="requestBody">The alert request body.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [HttpPost("ProcessAlertStream")]
    [HttpOptions("ProcessAlertStream")]
    public async Task ProcessAlertStream([FromBody] AlertRequestBody req)
    {
        if (req == null)
        {
            Response.StatusCode = 400;
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("Request body cannot be empty"));
            await Response.Body.FlushAsync();
            return;
        }

        try
        {
            Response.StatusCode = 200;
            Response.ContentType = "text/plain; charset=utf-8";

            _logger.LogInformation($"Agent Invoked with message - {JsonConvert.SerializeObject(req)}");

            if (string.IsNullOrEmpty(req.IncidentId))
            {
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("Invalid request body - IncidentId is required"));
                await Response.Body.FlushAsync();
                return;
            }

            if (!string.IsNullOrWhiteSpace(req.CustomAlertConfig?.AlertingId))
            {
                var incidentDetails = await _icmWorkflowClient.GetIncidentAsync(req.IncidentId);
                if (req.CustomAlertConfig.AlertingId != incidentDetails.MonitoringSlice)
                {
                    var errorMessage = $"The incident `{req.IncidentId}` was created by alert `{incidentDetails.MonitoringSlice}`, " +
                    $"not by the alert `{req.CustomAlertConfig.AlertingId}` you are editing, please try with a correct incident.\0";
                    await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(errorMessage));
                    await Response.Body.FlushAsync();
                    return;
                }
            }

            // Process the message using the injected chat service.
            var alertProcessingService = HttpContext.RequestServices.GetRequiredService<IAlertProcessingService>();
            var sessionMessageService = HttpContext.RequestServices.GetRequiredService<ISessionMessageService>();

            var pair = alertProcessingService.GetAlertProcessorAndSessionId(req);

            // Subscribe to the session messages and stream them to the client
            var task = sessionMessageService.Subscribe(pair.sessionId, async (message) =>
            {
                // Write each message to the response stream with a null terminator
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(message + "\0"));
                await Response.Body.FlushAsync();
            });

            // Process the alert
            await pair.processor.Invoke();

            // Wait for the subscription task to complete
            await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing alert stream: {Message}", ex.Message);
            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"Error processing alert stream: {ex.Message}"));
            }

            Response.StatusCode = 500;
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"Error processing alert stream: {ex.Message}"));
            return;
        }
    }

    [HttpPost("generateInstructions")]
    [HttpOptions("generateInstructions")]
    public async Task<IActionResult> GenerateInstructions([FromBody] GenerateInstructionsRequest request)
    {
        try
        {
            var result = await _instructionGenerationService.GenerateInstructions(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error generating instructions: {ex.Message}");
        }
    }

    [HttpGet("defaultIcmTeam")]
    [HttpOptions("defaultIcmTeam")]
    public async Task<IActionResult> GetDefaultTeam()
    {
        try
        {
            var defaultTeam = await _icmConfigService.GetDefaultIcmTeam();
            return Ok(defaultTeam);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving default team: {ex.Message}");
        }
    }

    [HttpGet("icmServices")]
    [HttpOptions("icmServices")]
    public async Task<IActionResult> GetIcmServices()
    {
        try
        {
            var services = await _icmConfigService.GetIcmServices();
            return Ok(services);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving ICM services: {ex.Message}");
        }
    }

    [HttpGet("icmTeams/{serviceId}")]
    [HttpOptions("icmTeams/{serviceId}")]
    public async Task<IActionResult> GetIcmTeams(int serviceId)
    {
        try
        {
            var teams = await _icmConfigService.GetIcmTeams(serviceId);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving ICM teams: {ex.Message}");
        }
    }


}
