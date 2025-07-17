using System.ComponentModel;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins;
public class HandoffToAgentPlugin
{
    private readonly ICMPlugin _icmPlugin;
    private readonly ILogger<HandoffToAgentPlugin> _logger;
    private readonly ITeamsClient _teamsClient;
    private readonly ISessionMessageService _sessionMessageService;
    private readonly AlertHandlerClient _alertHandlerClient;
    private readonly IHandoffToAgentClient _handoffToAgentClient;

    public HandoffToAgentPlugin(ICMPlugin icmPlugin, IHandoffToAgentClient handoffToAgentClient, ILogger<HandoffToAgentPlugin> logger, ITeamsClient teamsClient, ISessionMessageService sessionMessageService, AlertHandlerClient alertHandlerClient)
    {
        _icmPlugin = icmPlugin;
        _handoffToAgentClient = handoffToAgentClient;
        _logger = logger;
        _teamsClient = teamsClient;
        _sessionMessageService = sessionMessageService;
        _alertHandlerClient = alertHandlerClient;
    }

    [KernelFunction("get_configured_target_agents_for_handoff")]
    [Description("Get the list of configured target agents to which this agent can handoff processing. Returns a list of agent names.")]
    public List<string> GetConfiguredTargetAgentsForHandoff()
    {
        return _handoffToAgentClient.GetConfiguredTargetAgentsForHandoff();
    }

    [KernelFunction("handoff_to_another_icm_agent")]
    [Description("Hand off processing to another ICM agent. Fire and forget.")]
    public async Task<string> HandoffToAnotherICMAgent(
        Kernel kernel,
        [Description("Specifies the agent name to hand off to")]
            string targetAgentName,
        [Description("Incident ID")] string incidentId,
        [Description("Message to call the agent with")]
            string handoffMessage,
        [Description("Specifies the agent mode of the target agent to which processing will be handed off. Defaults to 'ICMAgent' if not provided.")]
            string agentMode = "ICMAgent")
    {
        if (string.IsNullOrWhiteSpace(targetAgentName))
        {
            string errorMessage = "Error: targetAgentName is null or empty.";
            await LogInformation("handoff_to_another_icm_agent", errorMessage, kernel);
            return errorMessage;
        }

        if (string.IsNullOrWhiteSpace(incidentId))
        {
            string errorMessage = "Error: incidentId is null or empty.";
            await LogInformation("handoff_to_another_icm_agent", errorMessage, kernel);
            return errorMessage;
        }

        if(_handoffToAgentClient.IsHandoffToAgentEnabled(targetAgentName).Item1 == false)
        {
            string errorMessage = $"Handoff to agent {targetAgentName} is disabled. Error: {_handoffToAgentClient.IsHandoffToAgentEnabled(targetAgentName).Item2}";
            await LogInformation("handoff_to_another_icm_agent", errorMessage, kernel);
            return errorMessage;
        }

        handoffMessage = string.IsNullOrWhiteSpace(handoffMessage) ? $"Request to process incident: {incidentId}" : handoffMessage;

        await LogInformation("handoff_to_another_icm_agent",
            $"Invoked for incidentId {incidentId} with handoff message {handoffMessage} for target agent {targetAgentName}", kernel);


        var incidentDetails = await _icmPlugin.GetIncidentInfo(incidentId, kernel);

        ICMAlertConfig alertConfig = await _alertHandlerClient.GetConfigAsync(incidentDetails, kernel);
        string senderAgentName = $"SREAgent_{alertConfig?.AgentName ?? "UnnamedAgent"}";

        await LogInformation("handoff_to_another_icm_agent", $"Handoff initiated for message: {handoffMessage} by {senderAgentName}", kernel);

        // Fire and forget handoff to another agent
        _ = _handoffToAgentClient.HandoffToAnotherICMAgentAsync(targetAgentName, incidentId, senderAgentName, handoffMessage, agentMode);

        return "Handoff initiated for message: " + handoffMessage;
    }

    private async Task LogInformation(string methodName, string message, Kernel kernel)
    {
        await kernel.LogInformation($"[{methodName}][{DateTime.UtcNow}] {message}", _logger, _teamsClient, _sessionMessageService);
    }
}
