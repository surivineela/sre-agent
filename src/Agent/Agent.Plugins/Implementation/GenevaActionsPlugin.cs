using System;
using Agent.Core.Configuration;
using Agent.Logging;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Models;
using Agent.Plugins.TeamsPlugin;
using FirstPartyAgent.Common.Configuration;
using k8s.KubeConfigModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Agent.Plugins;

public class GenevaActionsPlugin : IGenevaActionsPlugin
{
    private readonly ICMWorkflowClient _icmWorkflowClient;
    private readonly KustoClient _kustoClient;
    private readonly ILogger<GenevaActionsPlugin> _logger;
    private readonly ITeamsClient _teamsClient;
    private readonly CosmosClient _cosmosDBService;
    private readonly CosmosDBSettings _cosmosDBSettings;
    private readonly GenevaActionsSettings _genevaActionsSettings;

    private readonly bool _icmWorkflowReadOnly;

    private Lazy<Task<List<GenevaActionConfig>>> _lazyGenevaActions;


    public GenevaActionsPlugin(ICMWorkflowClient icmWorkflowClient, KustoClient kustoPlugin, ILogger<GenevaActionsPlugin> logger, ITeamsClient teamsClient, CosmosClient cosmosDBService, CosmosDBSettings cosmosDBSettings, GenevaActionsSettings genevaActionsSettings, ICMWorkflowSettings iCMWorkflowSettings)
    {
        _logger = logger;
        _icmWorkflowClient = icmWorkflowClient;
        _kustoClient = kustoPlugin;
        _teamsClient = teamsClient;
        _cosmosDBService = cosmosDBService;
        _cosmosDBSettings = cosmosDBSettings;
        _genevaActionsSettings = genevaActionsSettings;
        _icmWorkflowReadOnly = iCMWorkflowSettings.ReadOnly;
        _lazyGenevaActions = new Lazy<Task<List<GenevaActionConfig>>>(() => InitializeGenevaActionsConfig());
    }

    private async Task<List<GenevaActionConfig>> InitializeGenevaActionsConfig()
    {
        var allGenevaActions = new List<GenevaActionConfig>();
        _logger.LogInternalInformation("Initializing Geneva Actions Config");


        try
        {
            var genevaActionsContainer = _cosmosDBService.GetContainer(_cosmosDBSettings.Docs.Database, _genevaActionsSettings.CosmosDbContainerId);
            var queryDefinition = new QueryDefinition("SELECT * FROM c");
            var iterator = genevaActionsContainer.GetItemQueryIterator<GenevaActionsConfigCosmos>(queryDefinition);

            var genevaActionsConfig = new List<GenevaActionsConfigCosmos>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                genevaActionsConfig.AddRange(response);
            }

            allGenevaActions = genevaActionsConfig
                .SelectMany(c => c.GenevaActions)
                .GroupBy(a => a.ActionName)
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error reading alert details from CosmosDB: {ex.Message}");
        }

        return allGenevaActions;
    }

    private async Task<List<GenevaActionConfig>> GetGenevaActions()
    {
        return await _lazyGenevaActions.Value;
    }

    private async Task<string> ExecuteGenevaActionWorkflow(GenevaActionConfig genevaActionConfig, Dictionary<string, string> inputParameters)
    {
        var payload = JsonConvert.SerializeObject(inputParameters);
        var response = await _icmWorkflowClient.SendICMWorkflowRequest(genevaActionConfig.WorkflowName, payload, genevaActionConfig.TenantId);
        _logger.LogInternalInformation($"[execute_geneva_action_workflow] - workflowName: {genevaActionConfig.WorkflowName}, statusCode: {response.StatusCode}");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        else
        {
            string errorMessage = await response.Content.ReadAsStringAsync();
            return errorMessage;
        }
    }

    public async Task<string> ListInputParametersForGenevaAction(string actionName)
    {
        var logMessage = $"[list_input_parameters_for_geneva_action] Invoked with actionName {actionName}.";
        _logger.LogInternalInformation(logMessage);
        var genevaAction = (await GetGenevaActions()).Where(x => x.ActionName == actionName).FirstOrDefault();
        if (genevaAction == null)
        {
            return $"No Geneva Action found for actionName: {actionName}";
        }
        return $"For actionName: {actionName}. Required parameters are: {string.Join(", ", genevaAction.WorkflowInputParameters)}";
    }

    public async Task<string> ExecuteGenevaAction(string actionName, Dictionary<string, string> inputParameters)
    {
        var logMessage = $"[execute_geneva_action] Invoked with actionName {actionName} and parameters: {JsonConvert.SerializeObject(inputParameters)}";
        _logger.LogInternalInformation(logMessage);

        var genevaAction = (await GetGenevaActions()).Where(x => x.ActionName == actionName).FirstOrDefault();
        if (genevaAction == null)
        {
            return $"No Geneva Action found for actionName: {actionName}";
        }
        var paramsNotFound = genevaAction.WorkflowInputParameters.Where(x => !inputParameters.ContainsKey(x)).Any();
        if (paramsNotFound)
        {
            return $"Missing input parameters for actionName: {actionName}. Required parameters are: {string.Join(", ", genevaAction.WorkflowInputParameters)}";
        }

        if (_icmWorkflowReadOnly && genevaAction.IsWriteAction)
        {
            return "Success. ICM Workflow Client is in ReadOnly mode.";
        }

        var subscriptionId = inputParameters.ContainsKey("subscriptionId") ? inputParameters["subscriptionId"] : (inputParameters.ContainsKey("subscription") ? inputParameters["subscription"] : null);
        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            if (!genevaAction.IsAllowedOnExternalSubs && !(await IsSubscriptionInternal(subscriptionId)))
            {
                logMessage = $"[is_subscription_internal] The subscription {subscriptionId} is external. This action is not allowed.";
                _logger.LogInternalWarning(logMessage);
                return logMessage;
            }
        }

        _logger.LogInternalInformation("Proceeding with executing Geneva Action");
        return await ExecuteGenevaActionWorkflow(genevaAction, inputParameters);
    }

    private async Task<bool> IsSubscriptionInternal(string subscriptionId)
    {
        var logMessage = $"[is_subscription_internal] Checking if subscription {subscriptionId} is internal.";
        _logger.LogInternalInformation(logMessage);
        var kustoQuery = $@"DataStudio_ServiceTree_AzureSubscription_Snapshot
                | where SubscriptionId == '{subscriptionId}'
                | project ServiceName, SubscriptionId, ServiceId, Environment
                | take 1";

        var reader = await _kustoClient.PerformQueryAsync($"https://servicetreepublic.westus.kusto.windows.net", "Shared", kustoQuery);
        var kustoResult = new KustoQueryResult(reader, kustoQuery);
        if (!string.IsNullOrWhiteSpace(kustoResult.Result) && kustoResult.Result != "ZERO_ROWS_RETURNED")
        {
            var kustoResultDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(kustoResult.Result);
            if (kustoResultDictionary != null && kustoResultDictionary.Count > 0)
            {
                var subscriptionIdFromKusto = kustoResultDictionary["SubscriptionId"];
                if (subscriptionIdFromKusto == subscriptionId)
                {
                    return true; // Subscription is internal
                }
            }
        }
        return false;
    }
}
