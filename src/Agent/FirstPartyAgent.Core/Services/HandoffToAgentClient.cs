using System.Text;
using FirstPartyAgent.Core.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Services;
public interface IHandoffToAgentClient
{
    public List<string> GetConfiguredTargetAgentsForHandoff();
    public Tuple<bool, string> IsHandoffToAgentEnabled(string targetAgentName);
    public Task<string> HandoffToAnotherICMAgentAsync(string targetAgentName, string incidentId, string sender, string handoffMessage, string agentMode);
}

public class HandoffToAgentClient : IHandoffToAgentClient
{
    private readonly HandoffToAgentSettings _handoffToAgentSettings;
    private readonly ILogger<HandoffToAgentClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly int TimeoutInSeconds = 240; // Default timeout for handoff operations

    public HandoffToAgentClient(HandoffToAgentSettings handoffToAgentSettings, ILogger<HandoffToAgentClient> logger)
    {
        _handoffToAgentSettings = handoffToAgentSettings;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutInSeconds) };
    }

    public List<string> GetConfiguredTargetAgentsForHandoff()
    {
        if (_handoffToAgentSettings?.ConfiguredAgents?.Count() > 0)
        {
            return _handoffToAgentSettings.ConfiguredAgents.Where(kvp => kvp.Value?.IsDisabled == false)?.Select(kvp => kvp.Key)?.ToList() ?? new List<string>();
        }
        return new List<string>();
    }

    public Tuple<bool, string> IsHandoffToAgentEnabled(string targetAgentName)
    {
        if (_handoffToAgentSettings?.Enabled == true)
        {
            if (_handoffToAgentSettings.ConfiguredAgents.TryGetValue(targetAgentName, out var agentConfig))
            {
                if (agentConfig.IsDisabled)
                {
                    _logger.LogWarning($"Handoff to agent {targetAgentName} is disabled in configuration.");
                    return Tuple.Create(false, $"Handoff to agent {targetAgentName} is disabled in configuration.");
                }

                if (string.IsNullOrWhiteSpace(agentConfig.Endpoint) || string.IsNullOrWhiteSpace(agentConfig.AppKey))
                {
                    _logger.LogWarning($"Handoff configuration for agent {targetAgentName} is missing endpoint or app key.");
                    return Tuple.Create(false, $"Handoff configuration for agent {targetAgentName} is missing endpoint or app key.");
                }
                _logger.LogInformation($"Handoff to agent {targetAgentName} is enabled with endpoint {agentConfig.Endpoint}.");
                return Tuple.Create(true, agentConfig.Endpoint);
            }
            else
            {
                _logger.LogWarning($"Configuration necessary to handoff to {targetAgentName} agent is not set.");
                return Tuple.Create(false, $"Configuration necessary to handoff to {targetAgentName} agent is not set.");
            }
        }
        else
        {
            return Tuple.Create(false, "Handoff to agents is disabled in settings.");
        }
    }

    public async Task<string> HandoffToAnotherICMAgentAsync(string targetAgentName, string incidentId, string callingAgentName, string handoffMessage, string targetAgentMode)
    {
        var handoffCheck = IsHandoffToAgentEnabled(targetAgentName);
        if (handoffCheck.Item1 == false)
        {
            _logger.LogInformation($"Handoff to agent is disabled. Skipping handoff process. Error: {handoffCheck.Item2}");
            return $"Handoff to agent is disabled. Skipping handoff process. Error: {handoffCheck.Item2}";
        }

        _handoffToAgentSettings.ConfiguredAgents.TryGetValue(targetAgentName, out var agentConfig);
        if (agentConfig == null)
        {
            throw new InvalidOperationException($"Agent config for {targetAgentName} not found.");
        }
        var requestUri = agentConfig.Endpoint;
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
        var content = new
        {
            sender = callingAgentName,
            message = handoffMessage,
            agentMode = targetAgentMode,
            sessionId = $"ICMProcessing-{incidentId}"
        };

        requestMessage.Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");
        requestMessage.Headers.Add("x-functions-key", agentConfig.AppKey);

        try
        {
            var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = $"Target agent {targetAgentName} failed during handoff. Status Code: {response.StatusCode}, Error: {await response.Content.ReadAsStringAsync()}";
                response.Dispose();
                _logger.LogError(errorMessage);
                return errorMessage;
            }
            else
            {
                _logger.LogInformation($"Successfully handed off to agent {targetAgentName} for incident {incidentId}.");
                var message = $"Handoff to {targetAgentName} for incident {incidentId} by {callingAgentName} with message: {handoffMessage} successful. Response from handoff: {await response.Content.ReadAsStringAsync()}";
                _logger.LogInformation(message);
                response.Dispose();
                return message;
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while handing off to agent {targetAgentName} for incident {incidentId}.");
            return $"Error occurred while handing off to agent {targetAgentName} for incident {incidentId}. Exception: {ex.Message}";
        }
    }
}
