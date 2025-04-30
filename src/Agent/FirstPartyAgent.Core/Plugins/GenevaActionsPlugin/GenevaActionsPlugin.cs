// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Plugins
{
    public class GenevaActionsPlugin
    {
        private readonly BaseIcmWorkflowClient _icmWorkflowClient;
        private readonly IKustoPlugin _kustoPlugin;
        private readonly ILogger<GenevaActionsPlugin> _logger;
        private readonly ITeamsClient _teamsClient;
        private List<GenevaActionConfig> _allGenevaActions;
        private readonly IStorageService _storageService;
        private readonly ICosmosDBService _cosmosDbService;
        private ISessionMessageService _sessionMessageService;
        private string storageGenevaActionsContainerName = "genevaactionsconfig";
        private readonly string cosmosGenevaActionsContainerName = "GenevaActionsConfigs";
        private readonly string genevaActionsConfigName = "GenevaActions";

        public GenevaActionsPlugin(
            BaseIcmWorkflowClient icmWorkflowClient,
            IKustoPlugin kustoPlugin,
            ILogger<GenevaActionsPlugin> logger,
            ITeamsClient teamsClient,
            StorageAccountSettings storageAccountSettings,
            IStorageService storageService,
            ICosmosDBService cosmosDBService,
            ISessionMessageService sessionMessageService)
        {
            _sessionMessageService = sessionMessageService;
            _logger = logger;
            _icmWorkflowClient = icmWorkflowClient;
            _kustoPlugin = kustoPlugin;
            _teamsClient = teamsClient;
            _storageService = storageService;
            _cosmosDbService = cosmosDBService;
            _allGenevaActions = new List<GenevaActionConfig>();
            if (!string.IsNullOrWhiteSpace(storageAccountSettings.GenevaActionsContainerName))
            {
                storageGenevaActionsContainerName = storageAccountSettings.GenevaActionsContainerName;
            }
            InitializeGenevaActionsConfig().GetAwaiter().GetResult();
        }

        private async Task InitializeGenevaActionsConfig()
        {
            var logMessage = $"Initializing Geneva Actions Config";
            _logger.LogInformation(logMessage);

            if (_cosmosDbService != null && _cosmosDbService.IsEnabled)
            {
                try
                {
                    var genevaActionsContainer = _cosmosDbService.GetQueryableContainer<GenevaActionsConfigCosmos>(_cosmosDbService.IcmAgentDatabaseName, cosmosGenevaActionsContainerName);
                    var genevaActionsConfig = await genevaActionsContainer.ToListAsync();

                    _allGenevaActions = genevaActionsConfig
                        .SelectMany(c => c.GenevaActions)
                        .GroupBy(a => a.ActionName)
                        .Select(g => g.First())
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error reading alert details from CosmosDB: {ex.Message}");
                }
            }


            if (_storageService.IsEnabled)
            {
                try
                {
                    var genevaActionsConfigString = await _storageService.ReadFileFromStorage(storageGenevaActionsContainerName, "GenevaActions.json");
                    var genevaActionsConfig = JsonConvert.DeserializeObject<List<GenevaActionConfig>>(genevaActionsConfigString);
                    if (genevaActionsConfig != null && genevaActionsConfig.Count > 0)
                    {
                        _allGenevaActions.AddRange(genevaActionsConfig);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logMessage = $"Error reading Geneva Actions Config from Storage: {ex.Message}";
                    _logger.LogError(logMessage);
                    return;
                }
            }

            logMessage = $"Geneva Actions Config not found in CosmosDB or Storage. Reading from local file.";
            _logger.LogInformation(logMessage);

            try {
                var genevaActionsConfigFileContent = await File.ReadAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins/GenevaActionsPlugin/GenevaActions.json"));

                if (string.IsNullOrWhiteSpace(genevaActionsConfigFileContent))
                {
                    logMessage = $"Geneva Actions Config file is empty or not found.";
                    _logger.LogError(logMessage);
                    return;
                }

                var genevaActionsConfig = JsonConvert.DeserializeObject<List<GenevaActionConfig>>(genevaActionsConfigFileContent);
                if (genevaActionsConfig != null && genevaActionsConfig.Count > 0)
                {
                    _allGenevaActions.AddRange(genevaActionsConfig);
                    return;
                }
            }
            catch (Exception ex)
            {
                logMessage = $"Error reading or deserializing Geneva Actions Config: {ex.Message}";
                _logger.LogError(logMessage);
                return;
            }
        }

        private async Task<bool> IsSubscriptionInternal(string subscriptionId, Kernel kernel)
        {
            var logMessage = $"Checking if subscription {subscriptionId} is internal.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var kustoQuery = $@"DataStudio_ServiceTree_AzureSubscription_Snapshot
                | where SubscriptionId == '{subscriptionId}'
                | project ServiceName, SubscriptionId, ServiceId, Environment
                | take 1";
            var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery("servicetreepublic.westus", "Shared", kustoQuery, null, kernel);
            if (kustoResult != "ZERO_ROWS_RETURNED" && !string.IsNullOrWhiteSpace(kustoResult))
            {
                var kustoResultDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(kustoResult);
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

        private async Task<string> ExecuteGenevaActionWorkflow(GenevaActionConfig genevaActionConfig, Dictionary<string, string> inputParameters, Kernel kernel)
        {
            var payload = JsonConvert.SerializeObject(inputParameters);
            var response = await _icmWorkflowClient.SendICMWorkflowRequest(genevaActionConfig.WorkflowName, payload, genevaActionConfig.TenantId);

            var logMessage = $"ExecuteGenevaActionWorkflowStatus - workflowName: {genevaActionConfig.WorkflowName}, statusCode: {response.StatusCode}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);

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

        [KernelFunction("execute_geneva_action")]
        [Description("Execute a geneva action with action name, and input parameters")]
        public async Task<string> ExecuteGenevaAction(
           [Description("Action Name")] string actionName,
           [Description("Input Parameters")] Dictionary<string, string> inputParameters,
           Kernel kernel)
        {
            var logMessage = $"[execute_geneva_action][{DateTime.UtcNow}] Invoked with actionName {actionName} and parameters: {JsonConvert.SerializeObject(inputParameters)}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var genevaAction = _allGenevaActions.Where(x => x.ActionName == actionName).FirstOrDefault();
            if (genevaAction == null)
            {
                return $"No Geneva Action found for actionName: {actionName}";
            }
            var paramsNotFound = genevaAction.WorkflowInputParameters.Where(x => !inputParameters.ContainsKey(x)).Any();
            if (paramsNotFound)
            {
                return $"Missing input parameters for actionName: {actionName}. Required parameters are: {string.Join(", ", genevaAction.WorkflowInputParameters)}";
            }

            if (_icmWorkflowClient.ReadOnly && genevaAction.IsWriteAction)
            {
                return "Success. ICM Workflow Client is in ReadOnly mode.";
            }

            var subscriptionId = inputParameters.ContainsKey("subscriptionId") ? inputParameters["subscriptionId"] : (inputParameters.ContainsKey("subscription") ? inputParameters["subscription"] : null);
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                if (!genevaAction.IsAllowedOnExternalSubs && !(await IsSubscriptionInternal(subscriptionId, kernel)))
                {
                    logMessage = $"The subscription {subscriptionId} is external. This action is not allowed.";
                    await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
                    return logMessage;
                }
            }

            _logger.LogInformation($"Proceeding with executing Geneva Action");
            return await ExecuteGenevaActionWorkflow(genevaAction, inputParameters, kernel);
        }
    }
}

