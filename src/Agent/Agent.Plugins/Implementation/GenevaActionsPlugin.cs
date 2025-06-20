using System;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Models;
using Agent.Plugins.TeamsPlugin;
using k8s.KubeConfigModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;

namespace Agent.Plugins;

public class GenevaActionsPlugin : IGenevaActionsPlugin
{
    private readonly ICMWorkflowClient _icmWorkflowClient;
    private readonly KustoClient _kustoClient;
    private readonly ILogger<GenevaActionsPlugin> _logger;
    private readonly CosmosClient _cosmosDBService;
    private readonly CosmosDBSettings _cosmosDBSettings;
    private readonly GenevaActionsSettings _genevaActionsSettings;

    private readonly bool _icmWorkflowReadOnly;

    private Lazy<Task<List<GenevaActionConfig>>> _lazyGenevaActions;
    private OneBranchApprovalService _oneBranchApprovalService;

    public GenevaActionsPlugin(
        ICMWorkflowClient icmWorkflowClient,
        KustoClient kustoPlugin,
        ILogger<GenevaActionsPlugin> logger,
        CosmosClient cosmosDBService,
        CosmosDBSettings cosmosDBSettings,
        GenevaActionsSettings genevaActionsSettings,
        ICMWorkflowSettings iCMWorkflowSettings,
        OneBranchApprovalService oneBranchApprovalService)
    {
        _logger = logger;
        _icmWorkflowClient = icmWorkflowClient;
        _kustoClient = kustoPlugin;
        _cosmosDBService = cosmosDBService;
        _cosmosDBSettings = cosmosDBSettings;
        _genevaActionsSettings = genevaActionsSettings;
        _icmWorkflowReadOnly = iCMWorkflowSettings.ReadOnly;
        _lazyGenevaActions = new Lazy<Task<List<GenevaActionConfig>>>(() => InitializeGenevaActionsConfig());
        _oneBranchApprovalService = oneBranchApprovalService;
    }

    private async Task<List<GenevaActionConfig>> InitializeGenevaActionsConfig()
    {
        var allGenevaActions = new List<GenevaActionConfig>();
        _logger.LogInternalInformation("Initializing Geneva Actions Config");


        try
        {
            var genevaActionsContainer = _cosmosDBService
                .GetContainer(_cosmosDBSettings.Docs.Database, _genevaActionsSettings.CosmosDbContainerId)
                .GetItemLinqQueryable<AgentFactoryConfigCosmos<GenevaActionsConfigCosmos>>(true);


            var genevaActionsConfig = await genevaActionsContainer.Where(c => c.Id == "GenevaActionsConfig").ToListAsync();

            if(genevaActionsConfig == null || genevaActionsConfig.Count == 0)
            {
                _logger.LogInternalWarning("No Geneva Actions Config found in CosmosDB. Returning empty list.");
                return allGenevaActions;
            }

            allGenevaActions = genevaActionsConfig[0].Content.GenevaActions;
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

        if (_oneBranchApprovalService.IsEnabled && genevaAction.IsApprovalNeeded)
        {
            logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Geneva action requires approval. Creating approval document.";
            _logger.LogInternalInformation(logMessage);

            try
            {
                // Create approval request with detailed information about the action
                var approvalRequest = new OneBranchApprovalRequest
                {
                    CorrelationId = Guid.NewGuid().ToString(),
                    Title = $"Geneva Action Approval: {actionName}",
                    RequestDescription = $"Request to execute Geneva Action '{actionName}' with parameters: {JsonConvert.SerializeObject(inputParameters)}",
                    Submitter = "SRE Agent",
                    ServiceTreeGuid = genevaAction.ServiceTreeId?.ToString() ?? "00000000-0000-0000-0000-000000000000",
                    ReleaseApproversAllowed = new List<string> { "AME\\AZURE-ALL-PSV" } // FTE AME account, see https://dev.azure.com/mseng/AzureDevOps/_wiki/wikis/AzureDevOps.wiki/1113/TSG-Azure-Network-Troubleshooting?anchor=security-groups-that-you-need-to-join
                };

                // Create the approval document
                var approvalResponse = await _oneBranchApprovalService.CreateApprovalDocumentAsync(approvalRequest);

                logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Approval document created, please approve {approvalResponse.ApprovalDocumentUri} to continue.";
                _logger.LogInternalInformation(logMessage);

                // Poll for approval with binary exponential backoff
                var approvalStatus = await _oneBranchApprovalService.PollForApprovalAsync(approvalResponse.ApprovalDocumentId);

                string status = approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Action;
                if (status != "Approve")
                {
                    string message = $"Geneva Action execution was rejected by {approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Principal}. Status: {status}. Comments: {approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Comments}";
                    _logger.LogInternalInformation(message);
                    return message;
                }

                logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Geneva Action approved by {approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Principal}. Proceeding with execution.";
                _logger.LogInternalInformation(logMessage);
            }
            catch (Exception ex)
            {
                var errorMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Error in approval workflow: {ex.Message}";
                _logger.LogInternalWarning(errorMessage);
                return errorMessage;
            }
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


    private class AgentFactoryConfigCosmos<T>
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; }
        public T Content { get; set; }

        [JsonPropertyName("_ts")]
        [JsonProperty("_ts")]
        public int Timestamp { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTimeOffset Datetime => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
    }


}
public static class CosmosExtensions
{
    public async static Task<List<T>> ToListAsync<T>(this IQueryable<T> queryable)
    {
        var iterator = queryable.ToFeedIterator();
        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }
}
