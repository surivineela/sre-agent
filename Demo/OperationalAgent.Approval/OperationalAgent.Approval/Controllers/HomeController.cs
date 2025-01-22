using System.Diagnostics;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OperationalAgent.Approval.Models;

namespace OperationalAgent.Approval.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = configuration;
        }

        public IActionResult Index(string action_name)
        {
            ViewData["ActionName"] = action_name;
            return View();
        }

        [HttpPost("ProcessAction")]
        public async Task<IActionResult> ProcessAction([FromBody] ActionRequest request)
        {
            string nextPageMessage = string.Empty;
            string eventName = string.Empty;
            bool isValid = false;
            bool approvalSuccess = false;
            if (string.Equals(request.ActionName, "CheckAndDisableBasicAuth_instance", StringComparison.OrdinalIgnoreCase))
            {
                isValid = true;
                eventName = "DiableBasicAuthApprovalEvent";
            }
            else if(string.Equals(request.ActionName, "MonitorAvailability_instance", StringComparison.OrdinalIgnoreCase))
            {
                isValid = true;
                eventName = "ApproveMemoryDumpAndScaleUp";
            }

            if (isValid)
            {
                try
                {
                    string approvalEndpoint = string.Format(_config["OperationalRuntimeSendEventEndpoint"], request.ActionName, eventName);
                    var requestBody = JsonConvert.SerializeObject(request.IsApproved);
                    var response = await _httpClient.PostAsync(approvalEndpoint, new StringContent(requestBody, Encoding.UTF8, "application/json"));
                    approvalSuccess = request.IsApproved && response.IsSuccessStatusCode;
                    nextPageMessage = approvalSuccess ? "Action approved." : "Action Denied.";
                }
                catch
                {
                    approvalSuccess = false;
                    nextPageMessage = "Action Denied.";
                }
                string nextPage = approvalSuccess ? "Success": "Failure";

                return Ok(new { redirectUrl = Url.Action(nextPage, new { message = nextPageMessage }) });
            }

            return BadRequest(new { message = "Invalid action." });
        }

        public IActionResult Success(string message)
        {
            ViewData["Message"] = message;
            return View();
        }

        public IActionResult Failure(string message)
        {
            ViewData["Message"] = message;
            return View();
        }
    }

    public class ActionRequest
    {
        public string ActionName { get; set; }
        public bool IsApproved { get; set; }
    }
}
