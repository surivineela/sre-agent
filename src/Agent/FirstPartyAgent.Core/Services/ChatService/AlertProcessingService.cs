// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Services;

public interface IAlertProcessingService
{
    Task<ChatMessage> ProcessAlertAsync(AlertRequestBody alertRequest);

    (Func<Task<ChatMessage>> processor, string sessionId) GetAlertProcessorAndSessionId(AlertRequestBody alertRequest, bool test = false, string? sessionId = null);
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
    private readonly ISessionMessageService _sessionMessageService;

    public AlertProcessingService(
        IConfiguration config,
        ILogger<AlertProcessingService> logger,
        Kernel kernel,
        ITeamsClient teamsClient,
        ICMPlugin icmPlugin,
        AzureAlertingPlugin azureAlertingPlugin,
        IChatService chatService,
        ISessionMessageService sessionMessageService)
    {
        _icmPlugin = icmPlugin;
        _azureAlertingPlugin = azureAlertingPlugin;
        _config = config;
        _logger = logger;
        _kernel = kernel;
        _azureAlertingPlugin = azureAlertingPlugin;
        _chatService = chatService;
        _sessionMessageService = sessionMessageService;
    }

    private bool AgentModeExists(string agentMode)
    {
        return Enum.TryParse<AgentMode>(agentMode, out var mode);
    }

    private ChatMessage ApplyGuardrails(Incident incidentDetails)
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

        throw new Exception("No guardrail message to apply.");
    }

    private async Task<ChatMessage> ProcessAlertAsync(AlertRequestBody alertRequest, string sessionId, bool test = false)
    {
        if (alertRequest == null)
        {
            throw new ArgumentNullException(nameof(alertRequest), "AlertRequestBody cannot be null");
        }
        if (string.IsNullOrEmpty(alertRequest.IncidentId))
        {
            throw new ArgumentException("IncidentId cannot be empty", nameof(alertRequest.IncidentId));
        }

        if (!string.IsNullOrWhiteSpace(alertRequest.AgentMode))
        {
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

            var kernel = _kernel.Clone();
            kernel.Data["sessionId"] = sessionId;
            var incidentDetails = await _icmPlugin.GetIncidentInfo(alertRequest.IncidentId, kernel);

            if (!test)
            {
                var guardrailMessage = ApplyGuardrails(incidentDetails);
                if (guardrailMessage != null)
                {
                    await _sessionMessageService.GetPublisher(sessionId).Invoke(guardrailMessage.Message);
                    _sessionMessageService.DeleteSession(sessionId);
                    return guardrailMessage;
                }
            }

            var messageRequestBody = new MessageRequestBody()
            {
                Message = !string.IsNullOrWhiteSpace(alertRequest.CustomMessage) ? alertRequest.CustomMessage : $"A new Severity {incidentDetails.Severity} incident has been created. IncidentId - {alertRequest.IncidentId}",
                Sender = !string.IsNullOrWhiteSpace(alertRequest.Source) ? alertRequest.Source : "icm_automation",
                SessionId = sessionId,
                AgentMode = alertRequest.AgentMode,
            };

            if (alertRequest.CustomAlertConfig != null)
            {
                messageRequestBody.Data["customAlertConfig"] = alertRequest.CustomAlertConfig;
            }

            return await _chatService.ProcessMessageAsync(messageRequestBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in processing the alert.");
            if (ex.Message.Contains("HTTP 429 (429)"))
            {
                await Task.Delay(30000);
                alertRequest.CustomMessage = "Continue";
                return await ProcessAlertAsync(alertRequest, sessionId);
            }
            throw;
        }
    }

    public (Func<Task<ChatMessage>> processor, string sessionId) GetAlertProcessorAndSessionId(AlertRequestBody alertRequest, bool test = false, string? sessionId = null)
    {
        sessionId = sessionId ?? GetSessionId(alertRequest, test);
        return (() => ProcessAlertAsync(alertRequest, sessionId, test), sessionId);
    }

    public Task<ChatMessage> ProcessAlertAsync(AlertRequestBody alertRequest)
    {
        string sessionId = GetSessionId(alertRequest);
        return ProcessAlertAsync(alertRequest, sessionId);
    }

    private string GetSessionId(AlertRequestBody alertRequest, bool test = false)
    {
        if (test)
        {
            return Guid.NewGuid().ToString();
        }
        return $"ICMProcessing-{alertRequest.IncidentId}";
    }


}
