using Agent.Core;
using Agent.Core.Models;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Services;

public interface IAlertProcessingService
{
    Task<ChatMessage> ProcessAlertAsync(AlertRequestBody alertRequest);
}

public class AlertProcessingService : IAlertProcessingService
{
    private readonly IConfiguration _config;
    private readonly AsyncReaderWriterLock _lock = new();
    private readonly ILogger<AlertProcessingService> _logger;
    private readonly Kernel _kernel;
    private readonly ICMPlugin _icmPlugin;
    private readonly AzureAlertingPlugin _azureAlertingPlugin;
    private readonly IChatService _chatService;

    public AlertProcessingService(IConfiguration config, ILogger<AlertProcessingService> logger, Kernel kernel, ITeamsClient teamsClient, ICMPlugin icmPlugin, AzureAlertingPlugin azureAlertingPlugin, IChatService chatService)
    {
        _icmPlugin = icmPlugin;
        _azureAlertingPlugin = azureAlertingPlugin;
        _config = config;
        _logger = logger;
        _kernel = kernel;
        _azureAlertingPlugin = azureAlertingPlugin;
        _chatService = chatService;
    }

    private bool AgentModeExists(string agentMode)
    {
        return Enum.TryParse<AgentMode>(agentMode, out var mode);
    }

    private async Task<ChatMessage> ApplyGuardrails(Incident incidentDetails)
    {
        if (incidentDetails.Severity == "0" || incidentDetails.Severity == "1")
        {
            return new ChatMessage()
            {
                Message = $"Severity {incidentDetails.Severity} incident is not actionable. IncidentId - {incidentDetails.IncidentId}",
                Timestamp = DateTime.Now
            };
        }

        if (incidentDetails.Status == IncidentStatus.Resolved)
        {
            return new ChatMessage()
            {
                Message = $"Incident is already resolved. IncidentId - {incidentDetails.IncidentId}",
                Timestamp = DateTime.Now
            };
        }

        if (incidentDetails.CloudInstance?.ToLower() != "public")
        {
            return new ChatMessage()
            {
                Message = $"Incident is not in public cloud. IncidentId - {incidentDetails.IncidentId}",
                Timestamp = DateTime.Now
            };
        }

        if (incidentDetails.IncidentType != IncidentType.LiveSite)
        {
            return new ChatMessage()
            {
                Message = $"Incident is not a live site incident. IncidentId - {incidentDetails.IncidentId}",
                Timestamp = DateTime.Now
            };
        }

        //TODO: Check for incidents that have already been processed by the SRE Agent
        return null;
    }

    public async Task<ChatMessage> ProcessAlertAsync(AlertRequestBody alertRequest)
    {
        if (alertRequest == null) {
            throw new ArgumentNullException(nameof(alertRequest), "AlertRequestBody cannot be null");
        }
        if (string.IsNullOrEmpty(alertRequest.IncidentId))
        {
            throw new ArgumentException("IncidentId cannot be empty", nameof(alertRequest.IncidentId));
        }

        if (!string.IsNullOrWhiteSpace(alertRequest.AgentMode)) {
            var foundAgent = AgentModeExists(alertRequest.AgentMode);
            if (!foundAgent)
            {
                throw new ArgumentException($"Agent {alertRequest.AgentMode} not found", nameof(alertRequest.AgentMode));
            }
        }
        else
        {
            throw new ArgumentException("AgentMode cannot be empty");
        }

        try
        {
            //apply preprocessing, guardrails , etc.
            var incidentDetails = await _icmPlugin.GetIncidentInfo(alertRequest.IncidentId, _kernel);

            var guardrailMessage = await ApplyGuardrails(incidentDetails);
            if (guardrailMessage != null)
            {
                return guardrailMessage;
            }

            var messageRequestBody = new MessageRequestBody()
            {
                Message = !string.IsNullOrWhiteSpace(alertRequest.CustomMessage) ? alertRequest.CustomMessage : $"A new Severity {incidentDetails.Severity} incident has been created. IncidentId - {alertRequest.IncidentId}",
                Sender = !string.IsNullOrWhiteSpace(alertRequest.Source) ? alertRequest.Source : "icm_automation",
                SessionId = $"ICMProcessing-{alertRequest.IncidentId}",
                AgentMode = alertRequest.AgentMode,
            };
            return await _chatService.ProcessMessageAsync(messageRequestBody);

            /*//match incident details with existing alert configs
            if (incidentDetails.MonitoringRole == "AzureAlerting")
            {
                var alertId = incidentDetails.MonitoringSlice;
                var alertConfig = AgentFinder.GetICMAlertConfig(alertId);
                var alertDetails = await _azureAlertingPlugin.GetAlertDetailsById(alertId);

                var messageRequestBody = new MessageRequestBody()
                {
                    Message = !string.IsNullOrWhiteSpace(alertRequest.CustomMessage)? alertRequest.CustomMessage :  $"A new Severity {incidentDetails.Severity} incident has been created. IncidentId - {alertRequest.IncidentId}",
                    Sender = !string.IsNullOrWhiteSpace(alertRequest.Source) ? alertRequest.Source :  "icm_automation",
                    SessionId = $"ICMProcessing-{alertRequest.IncidentId}",
                    AgentMode = alertConfig.AgentMode ?? alertRequest.AgentMode,
                    PromptReplacements = new Dictionary<string, string>()
                    {
                        { "ALERT_DETAILS_HERE", JsonConvert.SerializeObject(alertDetails)},
                        { "CUSTOM_INSTRUCTIONS_HERE", string.Join("\n", alertConfig.MitigationInstructions) }
                    }
                };
                return await _chatService.ProcessMessageAsync(messageRequestBody);
            }
            else
            {
                var alertConfigs = AgentFinder.GetICMAlertConfigs();
                foreach (var alertId in alertConfigs.Keys) {
                    var alertConfig = alertConfigs[alertId];
                    if (incidentDetails.Title == alertConfig.IncidentTitle || (!string.IsNullOrWhiteSpace(alertConfig.IncidentTitleContains) && incidentDetails.Title.Contains(alertConfig.IncidentTitleContains, StringComparison.OrdinalIgnoreCase)))
                    {
                        var messageRequestBody = new MessageRequestBody()
                        {
                            Message = $"A new Severity {incidentDetails.Severity} incident has been created. IncidentId - {alertRequest.IncidentId}",
                            Sender = "icm_automation",
                            SessionId = $"ICMProcessing-{alertRequest.IncidentId}",
                            AgentMode = alertConfig.AgentMode ?? alertRequest.AgentMode,
                            PromptReplacements = new Dictionary<string, string>()
                            {
                                { "CUSTOM_INSTRUCTIONS_HERE", string.Join("\n", alertConfig.MitigationInstructions) }
                            }
                        };
                        return await _chatService.ProcessMessageAsync(messageRequestBody);
                    }
                }
            }

            var chatMessage = new ChatMessage()
            {
                Message = $"No matching alert configuration found for incidentId: {alertRequest.IncidentId}",
                Timestamp = DateTime.Now
            };

            return chatMessage;*/
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in processing the alert.");
            if (ex.Message.Contains("HTTP 429 (429)"))
            {
                await Task.Delay(30000);
                alertRequest.CustomMessage = "Continue";
                return await ProcessAlertAsync(alertRequest);
            }
            throw;
        }
    }
}