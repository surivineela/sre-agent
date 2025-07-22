// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FirstPartyAgent.FunctionApp
{
    public class ApiController
    {
        private readonly ILogger<ApiController> _logger;
        private readonly IChatService _chatService;
        private readonly IAlertProcessingService _alertProcessingService;
        private readonly ISessionMessageService _sessionMessageService;
        private readonly IStorageService _storageService;
        private readonly ICosmosDBService _cosmosDBService;
        private readonly IICMWorkflowClient _icmWorkflowClient;
        private readonly ITeamsClient _teamsClient;
        private readonly TeamsClientSettings _teamsClientSettings;
        private const string hotsiteAgentAlertDetailsCosmosDbContainer = "IcmAlertDetails";
        private readonly AlertHandlerService _alertHandlerService;
        private readonly TsgCrawlerClient _tsgCrawlerClient;

        public ApiController(
            ILogger<ApiController> logger,
            IChatService chatService,
            IStorageService storageService,
            IAlertProcessingService alertProcessingService,
            ISessionMessageService sessionMessageService,
            ICosmosDBService cosmosDBService,
            IICMWorkflowClient icmWorkflowClient,
            ITeamsClient teamsClient,
            TeamsClientSettings teamsClientSettings,
            AlertHandlerService alertHandlerService,
            TsgCrawlerClient tsgCrawlerClient)
        {
            _logger = logger;
            _chatService = chatService;
            _storageService = storageService;
            _alertProcessingService = alertProcessingService;
            _sessionMessageService = sessionMessageService;
            _cosmosDBService = cosmosDBService;
            _icmWorkflowClient = icmWorkflowClient;
            _teamsClient = teamsClient;
            _teamsClientSettings = teamsClientSettings;
            _alertHandlerService = alertHandlerService;
            _tsgCrawlerClient = tsgCrawlerClient;
        }

        [Function("ListConfigs")]
        public async Task<HttpResponseData> ListConfigs(
             [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ListConfigs")] HttpRequestData req, string alertId)
        {

            var configList = await _alertHandlerService.GetICMAlertConfigsAsync();
            var configInfo = configList.Select(x => new { x.Key, x.Value.AlertingId, x.Value.IncidentTitle, x.Value.AgentMode, x.Value.DefaultHumanInterventionLoop }).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(configInfo);
            return response;
        }

        [Function("GetConfig")]
        public async Task<HttpResponseData> GetConfig(
             [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetConfig/{alertId}")] HttpRequestData req, string alertId)
        {

            var customConfig = await _alertHandlerService.GetICMAlertConfigAsync(alertId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(customConfig);
            return response;
        }

        [Function("SetConfig")]
        public async Task<HttpResponseData> SetConfig(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "SetConfig")] HttpRequestData req)
        {
            var customConfig = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(customConfig))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Request body is empty" });
                return badResponse;
            }
            var configObject = JsonConvert.DeserializeObject<ICMAlertConfig>(customConfig);
            if (configObject == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid config object" });
                return badResponse;
            }
            await _alertHandlerService.SaveICMAlertConfig(configObject.AlertingId, customConfig);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(customConfig);
            return response;
        }

        [Function("GetAlertDetails")]
        public async Task<HttpResponseData> GetAlertDetails(
             [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetAlertDetails/{alertId}")] HttpRequestData req, string alertId)
        {
            try
            {
                if (_storageService.IsEnabled)
                {
                    var alertDetails = await _storageService.ReadFileFromStorage("alertdetails", $"{alertId}.json");
                    if (string.IsNullOrEmpty(alertDetails))
                    {
                        var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                        await notFoundResponse.WriteAsJsonAsync(new { error = "Alert details not found" });
                        return notFoundResponse;
                    }
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteStringAsync(alertDetails);
                    return response;
                }
                else
                {
                    var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AlertDetails");
                    var filePath = Path.Combine(folderPath, $"{alertId}.json");
                    if (!File.Exists(filePath))
                    {
                        var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                        await notFoundResponse.WriteAsJsonAsync(new { error = "Alert details not found" });
                        return notFoundResponse;
                    }
                    var alertDetails = File.ReadAllText(filePath);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteStringAsync(alertDetails);
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get alert details: {ex.Message}");
                var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await badResponse.WriteAsJsonAsync(new { error = "Failed to get alert details" });
                return badResponse;
            }
        }

        [Function("SaveAlertDetails")]
        public async Task<HttpResponseData> SaveAlertDetails(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "SaveAlertDetails")] HttpRequestData req)
        {
            var requestContent = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(requestContent))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Request body is empty" });
                return badResponse;
            }
            var alertDetails = JsonConvert.DeserializeObject<AlertDetailsBase>(requestContent);
            if (alertDetails == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badResponse;
            }
            var alertId = alertDetails.Id.ToString();
            if (_storageService.IsEnabled)
            {
                try
                {
                    await _storageService.WriteContentToStorage("alertdetails", $"{alertId}.json", requestContent);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteStringAsync("Success");
                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to save alert details: {ex.Message}");
                    var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await badResponse.WriteAsJsonAsync(new { error = "Failed to save alert details" });
                    return badResponse;
                }
            }
            else
            {
                try
                {
                    var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AlertDetails");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    var filePath = Path.Combine(folderPath, $"{alertId}.json");
                    File.WriteAllText(filePath, requestContent);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteStringAsync("Success");
                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to save alert details: {ex.Message}");
                    var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await badResponse.WriteAsJsonAsync(new { error = "Failed to save alert details" });
                    return badResponse;
                }
            }
        }


        [Function("SendMessage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "SendMessage")] HttpRequestData req)
        {
            // Deserialize the request body into your MessageRequestBody model.
            var requestBody = await req.ReadFromJsonAsync<MessageRequestBody>();
            {
                _logger.LogInformation($"Agent Invoked with message - {JsonConvert.SerializeObject(requestBody)}");
            }


            if (requestBody == null || string.IsNullOrEmpty(requestBody.Message))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badResponse;
            }

            if (requestBody.SendLogsToTeams && string.IsNullOrWhiteSpace(requestBody.SessionId))
            {
                if (_teamsClient.IsEnabled() && _teamsClientSettings.UseTeamsChannel)
                {
                    try
                    {
                        var sessionId = await _teamsClient.CreateTeamsChannelPost(new Agent.Core.Models.TeamsMessage($"Starting Processing for: {requestBody.Message}"));
                        requestBody.SessionId = sessionId.ToString();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to create post on TeamsChannel: {ex.Message}");
                    }
                }
            }

            // Process the message using the injected chat service.
            var chatResponse = await _chatService.ProcessMessageAsync(requestBody);

            // Return the processed response.
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(chatResponse);
            return response;
        }

        [Function("ProcessAlert")]
        public async Task<HttpResponseData> ProcessAlert(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ProcessAlert")] HttpRequestData req)
        {
            // Deserialize the request body into your AlertRequestBody model.
            var requestBody = await req.ReadFromJsonAsync<AlertRequestBody>();
            {
                _logger.LogInformation($"Agent Invoked with message - {JsonConvert.SerializeObject(requestBody)}");
            }


            if (requestBody == null || string.IsNullOrEmpty(requestBody.IncidentId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badResponse;
            }

            // Process the message using the injected chat service.
            var chatResponse = await _alertProcessingService.ProcessAlertAsync(requestBody);

            // Return the processed response.
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(chatResponse);
            return response;
        }

        [Function("ProcessAlertStream")]
        public async Task<HttpResponseData> ProcessAlertStream(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ProcessAlertStream")] HttpRequestData req)
        {
            _logger.LogInformation("Processing streaming ProcessAlert request");

            // Create a response with 200 OK status
            var response = req.CreateResponse(HttpStatusCode.OK);

            // Set content type
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            // Deserialize the request body into your AlertRequestBody model.
            var alertRequest = await req.ReadFromJsonAsync<AlertRequestBody>();
            {
                _logger.LogInformation($"Agent Invoked with message - {JsonConvert.SerializeObject(alertRequest)}");
            }

            if (alertRequest == null || string.IsNullOrEmpty(alertRequest.IncidentId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badResponse;                
            }

            if (alertRequest.CustomAlertConfig != null && !string.IsNullOrWhiteSpace(alertRequest.CustomAlertConfig.AlertingId))
            {
                var incidentDetails = await _icmWorkflowClient.GetIncidentAsync(alertRequest.IncidentId);
                if (alertRequest.CustomAlertConfig.AlertingId != incidentDetails.MonitoringSlice)
                {
                    await response.WriteStringAsync($"The incident `{alertRequest.IncidentId}` was created by alert `{incidentDetails.MonitoringSlice}`, " +
                        $"not by the alert `{alertRequest.CustomAlertConfig.AlertingId}` you are editing, please try with a correct incident.\0");
                    await response.Body.FlushAsync();
                    return response;
                }
            }

            // Process the message using the injected chat service.
            var pair = _alertProcessingService.GetAlertProcessorAndSessionId(alertRequest);
            var task = _sessionMessageService.Subscribe(pair.sessionId, async (message) =>
            {
                await response.WriteStringAsync(message + "\0");
                await response.Body.FlushAsync();
            });


            var chatResponse = await pair.processor.Invoke();

            await task;

            return response;
        }

        [Function("ImportAlertDetails")]
        public async Task<HttpResponseData> ImportAlertDetails(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ImportAlertDetails")] HttpRequestData req)
        {
            _logger.LogInformation("Processing ImportAlertDetails request");

            try
            {
                // Read file content from the request
                string fileContent = await new StreamReader(req.Body).ReadToEndAsync();

                if (string.IsNullOrEmpty(fileContent))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "File content is empty" });
                    return badResponse;
                }

                // Parse the file content into a list of AlertDetails
                List<WawsAlertDetails>? wawsAlertDetailsList;
                try
                {
                    wawsAlertDetailsList = JsonConvert.DeserializeObject<List<WawsAlertDetails>>(fileContent);

                    if (wawsAlertDetailsList == null || !wawsAlertDetailsList.Any())
                    {
                        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badResponse.WriteAsJsonAsync(new { error = "No valid AlertDetails found in file" });
                        return badResponse;
                    }
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    _logger.LogError($"Failed to parse file content: {ex.Message}");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = $"Failed to parse file content: {ex.Message}" });
                    return badResponse;
                }

                _logger.LogInformation($"Successfully parsed {wawsAlertDetailsList.Count} AlertDetails from file");

                var teamsJsonPath = Path.Combine(AppContext.BaseDirectory, "IcmTeams.json");
                var icmTeams = JsonConvert.DeserializeObject<List<IcmTeam>>(File.ReadAllText(teamsJsonPath));
                if (icmTeams == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "IcmTeams list is null" });
                    return badResponse;
                }
                var teamNameMap = icmTeams.ToDictionary(t => t.IcmTeamName.ToLower(), t => t.IcmTeamId);
                var alertDetails = wawsAlertDetailsList
                    .Where(a => a.Actions != null && a.Actions.Any(act => !string.IsNullOrWhiteSpace(act.TeamAssignedTo)))
                    .Select(a =>
                    {
                        var alertDetail = new AlertDetails(a);
                        var action = a.Actions?.FirstOrDefault(act => !string.IsNullOrWhiteSpace(act.TeamAssignedTo));
                        if (action != null)
                        {
                            alertDetail.TeamAssignedTo = action.TeamAssignedTo;
                            alertDetail.TeamId = teamNameMap.ContainsKey(action.TeamAssignedTo.ToLower()) ? teamNameMap[action.TeamAssignedTo.ToLower()] : null;
                            alertDetail.RoutingID = action.RoutingID;
                            alertDetail.Severity = action.Severity;
                        }
                        return alertDetail;
                    });

                foreach (var group in alertDetails.GroupBy(a => a.TeamId))
                {
                    await _cosmosDBService.BulkWriteAsync(
                        _cosmosDBService.IcmAgentDatabaseName,
                        hotsiteAgentAlertDetailsCosmosDbContainer,
                        group,
                        new Microsoft.Azure.Cosmos.PartitionKey(group.Key ?? 0));
                }


                var response = req.CreateResponse(HttpStatusCode.OK);


                await response.WriteStringAsync($"Successfully imported {alertDetails.Count()} alert details");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process ImportAlertDetails request: {ex.Message}");
                var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await badResponse.WriteAsJsonAsync(new { error = $"Failed to process request: {ex.Message}" });
                return badResponse;
            }
        }

        [Function("ImportGenevaActions")]
        public async Task<HttpResponseData> ImportGenevaActions(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ImportGenevaActions")] HttpRequestData req)
        {
            _logger.LogInformation("Processing ImportGenevaActions request");

            try
            {
                if (_cosmosDBService.CosmosClient == null)
                {
                    throw new InvalidOperationException("CosmosDB client is not initialized.");
                }
                var db = _cosmosDBService.CosmosClient.GetDatabase(_cosmosDBService.IcmAgentDatabaseName);
                var containerProperties = new ContainerProperties
                {
                    Id = "GenevaActionsConfigs",
                    PartitionKeyPath = "/TeamId",
                    UniqueKeyPolicy = new UniqueKeyPolicy
                    {
                        UniqueKeys =
                        {
                            new UniqueKey { Paths = { "/TeamId" } },
                        }
                    }
                };
                await db.CreateContainerIfNotExistsAsync(containerProperties);

                var config = await req.ReadFromJsonAsync<GenevaActionsConfigCosmos>();
                if (config == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Invalid GenevaActionsConfigCosmos object" });
                    return badResponse;
                }

                await _cosmosDBService.BulkWriteAsync(_cosmosDBService.IcmAgentDatabaseName,
                    "GenevaActionsConfigs",
                    new List<GenevaActionsConfigCosmos> { config },
                    new PartitionKey(config.TeamId));

                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process ImportGenevaActions request: {ex.Message}");
                var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await badResponse.WriteAsJsonAsync(new { error = $"Failed to process request: {ex.Message}" });
                return badResponse;
            }
        }

        [Function("ImportAgentDeployments")]
        public async Task<HttpResponseData> ImportAgentDeployments(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ImportAgentDeployments")] HttpRequestData req)
        {
            _logger.LogInformation("Processing ImportAgentDeployments request");
            try
            {
                if (_cosmosDBService.CosmosClient == null)
                {
                    throw new InvalidOperationException("CosmosDB client is not initialized.");
                }
                var db = _cosmosDBService.CosmosClient.GetDatabase(_cosmosDBService.IcmAgentDatabaseName);
                var containerProperties = new ContainerProperties
                {
                    Id = "AgentDeployments",
                    PartitionKeyPath = "/TeamId",
                };
                await db.CreateContainerIfNotExistsAsync(containerProperties);

                var data = await req.ReadFromJsonAsync<AgentDeployment>();
                if (data == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Invalid AgentDeployment object" });
                    return badResponse;
                }
                await _cosmosDBService.BulkWriteAsync(_cosmosDBService.IcmAgentDatabaseName,
                    "AgentDeployments",
                    new List<AgentDeployment> { data },
                    new PartitionKey(data.TeamId));
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process ImportAgentDeployments request: {ex.Message}");
                var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await badResponse.WriteAsJsonAsync(new { error = $"Failed to process request: {ex.Message}" });
                return badResponse;
            }
        }

        [Function("CrawlTsgRepository")]
        public async Task<HttpResponseData> CrawlTsgRepository(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "Crawl")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Starting TSG repository crawl process");
                await _tsgCrawlerClient.CrawlAndStoreRepositoryAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = "Repository crawl completed successfully" });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during repository crawl process: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { success = false, error = $"Error crawling repository: {ex.Message}" });
                return errorResponse;
            }
        }
    }
}

