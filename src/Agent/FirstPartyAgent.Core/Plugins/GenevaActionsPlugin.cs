// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins
{
    public class GenevaActionsPlugin
    {
        private readonly ICMWorkflowClient _icmWorkflowClient;
        private readonly IKustoPlugin _kustoPlugin;
        private readonly ILogger<GenevaActionsPlugin> _logger;
        private readonly ITeamsClient _teamsClient;

        public GenevaActionsPlugin(ICMWorkflowClient icmWorkflowClient, IKustoPlugin kustoPlugin, ILogger<GenevaActionsPlugin> logger, ITeamsClient teamsClient)
        {
            _logger = logger;
            _icmWorkflowClient = icmWorkflowClient;
            _kustoPlugin = kustoPlugin;
            _teamsClient = teamsClient;
        }

        private async Task<bool> IsSubscriptionInternal(string subscriptionId, Kernel kernel)
        {
            var logMessage = $"Checking if subscription {subscriptionId} is internal.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
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

        [KernelFunction("mark_subscription_as_first_party")]
        [Description("Mark Subscription as first party")]
        public async Task<string> MarkSubFirstParty(
           [Description("Subscription ID")] string subscriptionId,
           Kernel kernel)
        {
            var logMessage = $"[mark_subscription_as_first_party][{DateTime.UtcNow}] Invoked with subscriptionId {subscriptionId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            var isSubscriptionInternal = await IsSubscriptionInternal(subscriptionId, kernel);
            if (!isSubscriptionInternal)
            {
                return $"The subscription {subscriptionId} is external. Marking subscription as first party is not allowed.";
            }

            _logger.LogInformation($"Running Geneva Action to mark Subscription - {subscriptionId} as First Party.");
            return await _icmWorkflowClient.MarkSubFirstPartyAsync(subscriptionId);
        }

        [KernelFunction("get_subscription_details_from_geneva")]
        [Description("Get subscription details from geneva")]
        public async Task<string> GetSubDetailsFromGeneva(
           [Description("Subscription ID")] string subscriptionId, Kernel kernel)
        {
            var logMessage = $"[get_subscription_details_from_geneva][{DateTime.UtcNow}] Invoked with subscriptionId {subscriptionId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            return await _icmWorkflowClient.GetSubDetailsFromGenevaAsync(subscriptionId);
        }

        [KernelFunction("restart_web_app")]
        [Description("Restart Web App")]
        public async Task<string> RestartWebApp(
            [Description("Subscription Id")] string subscriptionId,
            [Description("WebApp Name")] string webappName,
            [Description("Webspace Name")] string webspaceName,
            Kernel kernel)
        {
            var isSubscriptionInternal = await IsSubscriptionInternal(subscriptionId, kernel);
            if (!isSubscriptionInternal)
            {
                return $"The subscription {subscriptionId} is external. Restarting web app is not allowed.";
            }
            var logMessage = $"[restart_webapp][{DateTime.UtcNow}] Invoked with subscriptionId {subscriptionId}, webAppName {webappName}, webspaceName {webspaceName}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            try
            {
                var restartOutput = await _icmWorkflowClient.RestartWebApp(subscriptionId, webappName, webspaceName);
                return restartOutput;
            }
            catch (Exception ex)
            {
                return $"Failed to restart web app for subscriptionId: {subscriptionId}, webappName: {webappName}, webspaceName: {webspaceName}";
            }
        }


        [KernelFunction("reboot_web_worker_for_webapp")]
        [Description("Reboot Web Worker for a Web app. Requires five parameters webAppName, stampName, location, role, roleInstance which can be fetched by getting webapp worker details")]
        public async Task<string> RebootWebWorkerForWebApp(
            [Description("Web App Name")] string webAppName,
            [Description("Stamp Name")] string stampName,
            [Description("Location")] string location,
            [Description("Role")] string role,
            [Description("Role Instance")] string roleInstance,
            Kernel kernel)
        {
            //TODO: Add some check here to confirm if the webapp is in internal subscription
            var logMessage = $"[reboot_worker][{DateTime.UtcNow}] Invoked for webAppName {webAppName}, location {location}, stampName {stampName}, role {role}, roleInstance {roleInstance}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            try
            {
                return await _icmWorkflowClient.RebootWorker(location, stampName, role, roleInstance);
            }
            catch (Exception ex)
            {
                return $"Failed to reboot the worker for for webAppName {webAppName}, location {location}, stampName {stampName}, role {role}, roleInstance {roleInstance}";
            }
        }
    }
}

