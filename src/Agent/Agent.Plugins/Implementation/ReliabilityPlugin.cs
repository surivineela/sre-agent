using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.ResourceGraph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using System.Data;
using System.Text.Json;
using Agent.Plugins.Interface;


namespace Agent.Plugins.Implementation
{
    public class ReliabilityPlugin : IReliabilityPlugin
    {
        public ILogger<ReliabilityPlugin> _logger;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly AzureResourceGraphClient _graphClient;
        private readonly ArmHelper _armHelper;
        private readonly Kernel _kernel;
        private readonly IGraphDBPlugin _graphDBPlugin;

        public ReliabilityPlugin(Kernel kernel, IGraphDBPlugin graphDBPlugin, IChatCompletionService chatCompletionService, AzureResourceGraphClient graphClient, ArmHelper armHelper, ILogger<ReliabilityPlugin> logger)
        {
            _chatCompletionService = chatCompletionService;
            _logger = logger;
            _graphClient = graphClient;
            _armHelper = armHelper;
            _kernel = kernel;
            _graphDBPlugin = graphDBPlugin;
        }

        // updates AlwaysOn property of the app service to true
        public async Task<string> UpdateAlwaysOn(string resourceId, bool enabled)
        {
            try
            {
                _logger.LogInternalInformation("Invoked UpdateAlwaysOn function");

                bool success = await _armHelper.UpdateAlwaysOn(resourceId, enabled);

                var message = success switch
                {
                    true => $"Resource {resourceId}'s alwaysOn updated to true at {DateTime.UtcNow:o}",
                    false => $"Failed to update resource {resourceId}'s alwaysOn to true at {DateTime.UtcNow:o}",
                };

                _logger.LogInternalInformation(message);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"UpdateAlwaysOn failed: {ex.ToString()}");
                throw;
            }
        }

        // updates HealthCheck property of the app service
        public async Task<string> UpdateHealthCheck(string resourceId, string healthCheckPath)
        {
            try
            {
                _logger.LogInternalInformation("Invoked UpdateHealthCheck function");

                bool success = false;

                success = await _armHelper.UpdateHealthcheck(resourceId, healthCheckPath);

                var message = success switch
                {
                    true => $"Resource {resourceId}'s healthCheckPath updated to {healthCheckPath} at {DateTime.UtcNow:o}",
                    false => $"Failed to update resource {resourceId}'s healthCheckPath to {healthCheckPath} at {DateTime.UtcNow:o}",
                };

                _logger.LogInternalInformation(message);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"UpdateHealthCheck failed: {ex.ToString()}");
                throw;
            }
        }

        // updates AutoHeal property of the app service
        public async Task<string> UpdateAutoHeal(string resourceId, bool autoHealEnabled, AutoHealRules autoHealRules)
        {
            try
            {
                _logger.LogInternalInformation("Invoked UpdateAutoHeal function");

                bool success = await _armHelper.UpdateAutoHeal(resourceId, autoHealEnabled, autoHealRules);

                var message = success switch
                {
                    true => $"Resource {resourceId}'s autoHeal updated to {autoHealEnabled} at {DateTime.UtcNow:o}",
                    false => $"Failed to update resource {resourceId}'s autoHeal to {autoHealEnabled} at {DateTime.UtcNow:o}",
                };

                _logger.LogInternalInformation(message);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"UpdateAutoHeal failed: {ex.ToString()}");
                throw;
            }

        }

        // changes the number of workers that the app service is hosted on
        public async Task<string> UpdateHostWorkers(string resourceId, int numberOfWorkers)
        {
            try
            {
                _logger.LogInternalInformation("Invoked UpdateHostWorkers function");

                bool success = await _armHelper.UpdateNumberOfWorkersAppService(resourceId, numberOfWorkers);

                var message = success switch
                {
                    true => $"Resource {resourceId} is now hosted on {numberOfWorkers} Workers at {DateTime.UtcNow:o}",
                    false => $"Failed to change resource {resourceId}'s number of Workers to {numberOfWorkers} at {DateTime.UtcNow:o}",
                };

                _logger.LogInternalInformation(message);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"UpdateHostWorkers failed: {ex.ToString()}");
                throw;
            }
        }

        // returns the Reliability properties of the app service
        public async Task<string> GetReliabilityStatus(string resourceId)
        {
            _logger.LogInternalInformation("Invoked GetReliabilityStatus function");

            CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            return await GetReliabilityOfAppService(_kernel, resourceId, tokenSource.Token);
        }

        // returns the Reliability properties of the app services
        public async Task<string> GetReliabilityStatus(string[] resourceIds)
        {
            _logger.LogInternalInformation("Invoked GetReliabilityStatus function");

            CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            return await GetReliabilityOfAppServices(_kernel, resourceIds, tokenSource.Token);
        }

        public async Task<string> GetReliabilityStatusForSubscriptions(CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation("Invoked GetReliabilityStatusForSubscriptions function");
            try
            {
                var subs = await _graphDBPlugin.ListSubscriptionsAsync();
                var ReliabilityTables = new List<Tuple<string, DataTable>>();

                foreach (var sub in subs)
                {
                    ResourceQueryResult webAppsQuery = await _graphClient.Query(
                    new[] { (string)sub },
                    $"Resources | where type =~ 'Microsoft.Web/sites' " +
                    "| extend serverFarmId = tostring(properties.serverFarmId), virtualNetworkSubnetId = tostring(properties.virtualNetworkSubnetId) " +
                    "| project id, type, subscriptionId, resourceGroup, name, location, serverFarmId, virtualNetworkSubnetId," +
                    "    numberOfWorkers = properties.siteConfig.numberOfWorkers," +
                    "    autoHealEnabled = isnotnull(properties.siteConfig.autoHealEnabled)," +
                    "    alwaysOn = properties.siteConfig.alwaysOn," +
                    "    healthCheckEnabled = isnotnull(properties.siteConfig.healthCheckPath)");

                    _logger.LogDebug($"Found {webAppsQuery.Count} web apps under {sub}");
                    var webAppsJson = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(webAppsQuery.Data);
                    var table = new DataTable();
                    table.Columns.Add("App Name", typeof(string));
                    table.Columns.Add("ARM resource ID", typeof(string));
                    table.Columns.Add("Number of Workers", typeof(int));
                    table.Columns.Add("AutoHealEnabled", typeof(bool));
                    table.Columns.Add("AlwaysOn", typeof(bool));
                    table.Columns.Add("HealthCheckEnabled", typeof(bool));

                    foreach (var item in webAppsJson.EnumerateArray())
                    {
                        var webAppNode = CreateNodeFromJsonForAppSerivceReliability(item);
                        table.Rows.Add(webAppNode?.ResourceName, webAppNode?.ResourceId, webAppNode?.NumberOfWorkers, webAppNode?.AutoHealEnabled, webAppNode?.AlwaysOn, webAppNode?.HealthCheckEnabled);
                        ReliabilityTables.Add(new Tuple<string, DataTable>(sub, table));
                    }
                }

                string userQuery = $"Format these apps into a table with the columns App Name, Number of Workers, Auto Heal Enabled, Always On Enabled, Health Check Enabled. ** Only return the table. ** \r\n {JsonConvert.SerializeObject(ReliabilityTables)}";
                var response = await _chatCompletionService.GetChatMessageContentAsync(userQuery);
                _logger.LogInternalInformation(response?.Content ?? "");
                return response?.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetReliabilityStatusForSubscriptions failed: {ex.ToString()}");
                throw;
            }
        }

        public async Task<string> GetAppsToMonitor(CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation("Invoked GetAppsToMonitor function");
            var appsToMonitor = new List<AppReliability>();
            try
            {
                var subs = await _graphDBPlugin.ListSubscriptionsAsync();

                foreach (var sub in subs)
                {
                    ResourceQueryResult webAppsQuery = await _graphClient.Query(
                    new[] { (string)sub },
                    $"Resources | where type =~ 'Microsoft.Web/sites' " +
                    "| extend serverFarmId = tostring(properties.serverFarmId), virtualNetworkSubnetId = tostring(properties.virtualNetworkSubnetId) " +
                    "| project id, type, subscriptionId, resourceGroup, name, location, serverFarmId, virtualNetworkSubnetId," +
                    "    numberOfWorkers = properties.siteConfig.numberOfWorkers," +
                    "    autoHealEnabled = isnotnull(properties.siteConfig.autoHealEnabled)," +
                    "    alwaysOn = properties.siteConfig.alwaysOn," +
                    "    healthCheckEnabled = isnotnull(properties.siteConfig.healthCheckPath)");

                    _logger.LogDebug($"Found {webAppsQuery.Count} web apps under {sub}");
                    var webAppsJson = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(webAppsQuery.Data);

                    foreach (var item in webAppsJson.EnumerateArray())
                    {
                        var webAppNode = CreateNodeFromJsonForAppSerivceReliability(item);
                        appsToMonitor.Add(new AppReliability(webAppNode?.ResourceId ?? string.Empty, webAppNode?.AlwaysOn ?? false, webAppNode?.HealthCheckEnabled ?? false, webAppNode?.AutoHealEnabled ?? false, webAppNode?.NumberOfWorkers ?? 1));
                    }
                }
                var response = JsonConvert.SerializeObject(appsToMonitor);
                _logger.LogInternalInformation(response);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetAppsToMonitor failed: {ex.ToString()}");
                throw;
            }
        }

        private async Task<string> GetReliabilityOfAppService(
            Kernel kernel,
            string resourceId,
            // TODO: use the token to cancel operation
            CancellationToken cancellationToken = default
        )
        {
            var chatHistory = new ChatHistory();

            chatHistory.AddSystemMessage(
                $@"You are an Azure App Service expert analyzing the Reliability of the App Service instance.
                Follow these instructions:
                  1. Find the app service using the graph traversal query agent.
                    a. *** Specifically run g.V().has('resourceName', '{resourceId}').has('isDeleted', false) ***
                    b. *** If 1a doesn't return any results, then specifically run g.V().has('resourceId', '{resourceId}').has('isDeleted', false) ***
                2. Once you have the app service, find the app's properties, relating to Reliability (autoheal, healthcheck, number of VMS, alwaysOn)
                    a. *** Specifically run g.V().has('resourceName', '{resourceId}').has('isDeleted', false).properties() ***
                3. Return the following key metrics in table format with five columns. The first column is the App Name (name of app). The second column is AutoHealEnabled.
                   The third column is HealthCheckEnabled. The fourth column is NumberOfWorkers. The fifth column is AlwaysOn.
                  � AutoHealEnabled (boolean: true or false)
                  � HealthCheckEnabled (boolean: true or false)
                  � NumberOfWorkers (int)
                  � AlwaysOn (boolean: true or false)
                "
            );
            var result = await _chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
                kernel: kernel,
                cancellationToken: cancellationToken);

            return result.Content ?? string.Empty;
        }

        private async Task<string> GetReliabilityOfAppServices(
            Kernel kernel,
            string[] resourceIds,
            // TODO: use the token to cancel operation
            CancellationToken cancellationToken = default
        )
        {
            var chatHistory = new ChatHistory();

            chatHistory.AddSystemMessage(
                $@"You are an Azure App Service expert analyzing the Reliability of the App Service instances.
                Follow these instructions:
                  1. For each resourceId in the list of resourceIds {resourceIds}, find the app services using the graph traversal query agent.
                    a. *** Specifically run g.V().has('resourceName', resourceId).has('isDeleted', false) ***
                    b. *** If 1a doesn't return any results, then specifically run g.V().has('resourceId', resourceId).has('isDeleted', false) ***
                2. Once you have the app services, find the apps' properties, relating to Reliability (autoheal, healthcheck, number of VMS, alwaysOn)
                    a. *** Specifically run g.V().has('resourceName', resourceId).has('isDeleted', false).properties() for each resourceId***
                3. Return the following key metrics in table format with five columns. The first column is the App Name (name of app). The second column is AutoHealEnabled.
                   The third column is HealthCheckEnabled. The fourth column is NumberOfWorkers. The fifth column is AlwaysOn.
                  � AutoHealEnabled (boolean: true or false)
                  � HealthCheckEnabled (boolean: true or false)
                  � NumberOfWorkers (int)
                  � AlwaysOn (boolean: true or false)
                "
            );
            var result = await _chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
                kernel: kernel,
                cancellationToken: cancellationToken);

            return result.Content ?? string.Empty;
        }

        private async Task<AutoHealRules> GetAutoHealRules(
            Kernel kernel,
            string resourceId,
            // TODO: use the token to cancel operation
            CancellationToken cancellationToken = default
        )
        {
            var chatHistory = new ChatHistory();

            // contain logic for sequence of questions to determine autohealrules 
            chatHistory.AddSystemMessage(
                $@"Ask the user if they want to enable or disable autoheal on the app service {resourceId}.
                If the user wants to enable autoheal, ask them if they would like to add more autoheal rules. Else, return an empty string.
                
                1. Ask what type of action the user would like to take on the app service (Custom Action, Log Event, or to Recycle)
                    a. If it's a custom action, ask the user what's the name of the executable file they want to run, as well as any parameters that come along with it
                    b. What's the minimum amount of time for the process to execute
                2. Ask what types of triggers the user would like to enable for autoheal from the following:
                    \n PrivateBytesInKB (threshold of memory to exceed)
                    \n Requests (count of requests in a certain time interval)
                    \n SlowRequests (number of requests to a certain path that take longer than a certain duration)
                    \n SlowRequestsWithPath (multiple requests that took longer than a certain duration)
                    \n StatusCodes (status codes returned from a path in a certain time period)
                    \n StatusCodesRange (range of status codes returned in a certain time interval
                3. Return the AutoHealRules object as a json object
                "
            );

            var result = await _chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
                kernel: kernel);

            return JsonConvert.DeserializeObject<AutoHealRules>(result.Content ?? string.Empty)!;
        }

        private AppServiceNode? CreateNodeFromJsonForAppSerivceReliability(JsonElement item)
        {
            try
            {
                var resourceId = item.GetProperty("id").GetString() ?? string.Empty;
                var resourceType = item.GetProperty("type").GetString() ?? string.Empty;
                var subscriptionId = item.GetProperty("subscriptionId").GetString() ?? string.Empty;
                var resourceGroupName = item.GetProperty("resourceGroup").GetString() ?? string.Empty;
                var resourceName = item.GetProperty("name").GetString() ?? string.Empty;
                var location = item.GetProperty("location").GetString() ?? string.Empty;

                var node = new AppServiceNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location);

                if (item.TryGetProperty("numberOfWorkers", out var numberOfWorkersValue))
                {
                    node.NumberOfWorkers = numberOfWorkersValue.GetInt32();
                }
                if (item.TryGetProperty("autoHealEnabled", out var autoHealValue))
                {
                    node.AutoHealEnabled = autoHealValue.GetInt32() > 0;
                }
                if (item.TryGetProperty("alwaysOn", out var alwaysOnValue))
                {
                    node.AlwaysOn = alwaysOnValue.GetBoolean();
                }
                if (item.TryGetProperty("healthCheckEnabled", out var healthCheckEnabledValue))
                {
                    node.HealthCheckEnabled = healthCheckEnabledValue.GetInt32() > 0;
                }

                return node;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"Error creating node from JSON: {ex.Message}");
                return null;
            }
        }
    }
}
