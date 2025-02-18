using Agent.Core.Models;
using FirstPartyAgent.Agents;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using System.Text.RegularExpressions;

namespace FirstPartyAgent.Web.Controllers;

[Route("api/")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly ILogger<ApiController> _logger;
    private readonly Kernel _kernel;
    private readonly IQuotaAgentService _quotaAgentService;
    private readonly IIcmPlugin _icmPlugin;
    private readonly ITaskStorageService _taskStorageService;
    public ApiController(ILogger<ApiController> logger, Kernel kernel, IQuotaAgentService quotaAgentService, IIcmPlugin icmPlugin, ITaskStorageService taskStorageService)
    {
        _logger = logger;
        _kernel = kernel;
        _quotaAgentService = quotaAgentService;
        _icmPlugin = icmPlugin;
        _taskStorageService = taskStorageService;
    }
    [Route("UpdatePrompt")]
    [HttpPost]
    public async Task<IActionResult> UpdatePrompt(Prompt prompt)
    {
        Prompts.QuotaAgent = prompt.Text;
        return Ok();
    }

    [Route("UpdateTeamsMessage")]
    [HttpPost]
    public async Task<ObjectResult> UpdateTeamsMessage(TeamsMessage message)
    {
        // TODO: This is a workaround for receive the message from Teams and process it.
        // It is becasue we store the incident data in Azure Queue cannot be retrived anytime.
        if (message.Title != null)
        {
            // TODO: Workaround for getting Incident ID
            var match = Regex.Match(message.Title, "\\[AI Generated\\]\\[(\\d+)\\]");
            if (match.Success)
            {
                var incidentId = match.Groups[1].Value;
                var content = $"[From Teams] <b>{message.User}</b> said: {message.Content}";
                var incident = await _icmPlugin.AddDiscussionEntry(incidentId, content);
                var allTasks = await _taskStorageService.GetAllTasksAsync();
                QuotaIncidentState status;
                allTasks.TryGetValue(incidentId, out status);
                if (status != null)
                {
                    var state = await _quotaAgentService.Process(status, new List<Disscussion> { new Disscussion(message.User, DiscussionSource.Teams, message.Content) });
                    return Ok(state);
                }
            }
        }
        return Ok(message);
    }
    [Route("ProcessQuotaIncident")]
    [HttpPost]
    public async Task<ObjectResult> ProcessQuotaIncident(ProcessQuotaIncidentRequest req)
    {
        try
        {
            var state = await _quotaAgentService.Process(req, req.Discussions);
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            throw;
        }
    }
}
public class Prompt
{
    public string Text { get; set; }
}