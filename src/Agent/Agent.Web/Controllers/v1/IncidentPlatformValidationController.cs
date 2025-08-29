using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class IncidentPlatformValidationController : ControllerBase
    {
        private readonly ILogger<IncidentPlatformValidationController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        private const string ServiceNowUri = "/api/now/v1/table/incident?sysparm_limit=1";

        public IncidentPlatformValidationController(
            ILogger<IncidentPlatformValidationController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public class ServiceNowValidationRequest
        {
            [Required]
            public string Endpoint { get; set; } = string.Empty;

            [Required]
            public string Username { get; set; } = string.Empty;

            [Required]
            public string Password { get; set; } = string.Empty;
        }

        public class ValidationResponse
        {
            public string Result { get; set; } = string.Empty;
            public string? ErrorMessage { get; set; }
        }

        [HttpPost("servicenow")]
        [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
        public async Task<IActionResult> ValidateServiceNow([FromBody] ServiceNowValidationRequest request)
        {
            try
            {
                _logger.LogInternalInformation("Starting ServiceNow validation for endpoint: {Endpoint}", request.Endpoint);

                if (string.IsNullOrWhiteSpace(request.Endpoint))
                {
                    return Ok(new ValidationResponse { Result = "missingEndpoint" });
                }

                if (string.IsNullOrWhiteSpace(request.Username))
                {
                    return Ok(new ValidationResponse { Result = "missingUsername" });
                }

                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    return Ok(new ValidationResponse { Result = "missingPassword" });
                }

                // Clean up endpoint URL
                string cleanEndpoint = request.Endpoint.Replace("https://", "").Replace("http://", "").TrimEnd('/');
                var baseUrl = new Uri($"https://{cleanEndpoint}");

                // Create basic auth header
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{request.Username}:{request.Password}"));

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.BaseAddress = baseUrl;
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                _logger.LogInternalInformation($"Making HTTP request to ServiceNow endpoint: {ServiceNowUri}");

                var response = await httpClient.GetAsync(ServiceNowUri);

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInternalInformation("ServiceNow validation successful for endpoint: {Endpoint}", request.Endpoint);
                    return Ok(new ValidationResponse { Result = "valid" });
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogInternalWarning("ServiceNow validation failed: Invalid credentials for endpoint: {Endpoint}", request.Endpoint);
                    return Ok(new ValidationResponse { Result = "invalidCredentials" });
                }

                _logger.LogInternalWarning("ServiceNow validation failed with status code: {StatusCode} for endpoint: {Endpoint}",
                    response.StatusCode, request.Endpoint);
                return Ok(new ValidationResponse { Result = "unknownError", ErrorMessage = $"HTTP {response.StatusCode}" });
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Name or service not known") ||
                                                   ex.Message.Contains("No such host is known") ||
                                                   ex.Message.Contains("The remote name could not be resolved"))
            {
                _logger.LogInternalWarning("ServiceNow validation failed: Connection error for endpoint: {Endpoint}. Error: {Error}",
                    request.Endpoint, ex.Message);
                return Ok(new ValidationResponse { Result = "connectionError", ErrorMessage = ex.Message });
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogInternalWarning("ServiceNow validation failed: Timeout for endpoint: {Endpoint}", request.Endpoint);
                return Ok(new ValidationResponse { Result = "connectionError", ErrorMessage = "Request timed out" });
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "ServiceNow validation failed with unexpected error for endpoint: {Endpoint}", request.Endpoint);
                return Ok(new ValidationResponse { Result = "unknownError", ErrorMessage = ex.Message });
            }
        }
    }
}
