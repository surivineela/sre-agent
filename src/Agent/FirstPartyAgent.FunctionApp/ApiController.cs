using Agent.Core.Helpers;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Threading.Tasks;

namespace FirstPartyAgent.FunctionApp
{
    public class ApiController
    {
        private readonly ILogger<ApiController> _logger;
        private readonly IChatService _chatService;
        private readonly IAlertProcessingService _alertProcessingService;
        private readonly IStorageService _storageService;

        public ApiController(ILogger<ApiController> logger, IChatService chatService, IStorageService storageService, IAlertProcessingService alertProcessingService)
        {
            _logger = logger;
            _chatService = chatService;
            _storageService = storageService;
            AgentFinder.SetStorageService(storageService);
            _alertProcessingService = alertProcessingService;
        }

        [Function("ListConfigs")]
        public async Task<HttpResponseData> ListConfigs(
             [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ListConfigs")] HttpRequestData req, string alertId)
        {

            var configList = AgentFinder.GetICMAlertConfigs();
            var configInfo = configList.Select(x => new { x.Key, x.Value.AlertingId, x.Value.IncidentTitle, x.Value.AgentMode, x.Value.DefaultHumanInterventionLoop }).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(configInfo);
            return response;
        }

        [Function("GetConfig")]
        public async Task<HttpResponseData> GetConfig(
             [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetConfig/{alertId}")] HttpRequestData req, string alertId)
        {

            var customConfig = AgentFinder.GetICMAlertConfig(alertId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(customConfig);
            return response;
        }

        [Function("SetConfig")]
        public async Task<HttpResponseData> SetConfig(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "SetConfig")] HttpRequestData req)
        {
            var customConfig = await req.ReadAsStringAsync();
            var configObject = JsonConvert.DeserializeObject<ICMAlertConfig>(customConfig);
            await AgentFinder.SaveICMAlertConfig(configObject.AlertingId, customConfig);
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
            var alertDetails = JsonConvert.DeserializeObject<AlertDetails>(requestContent);
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
    }
}
