using System.Diagnostics;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Http;
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
        private const string EasyAuthUserHeader = "X-MS-CLIENT-PRINCIPAL-NAME";

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = configuration;
        }

        public IActionResult Index(string action_name)
        {
            string userName = Request.Headers[EasyAuthUserHeader].FirstOrDefault();
            ViewData["UserName"] = string.IsNullOrEmpty(userName) ? "Unknown User" : userName;

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
                eventName = "DisableBasicAuthApprovalEvent";
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
                    var payload = new
                    {
                        approvalAction = request.IsApproved,
                        decisionMakerName = request.ApproverName
                    };

                    string approvalEndpoint = string.Format(_config["OperationalRuntimeSendEventEndpoint"], request.ActionName, eventName);
                    var requestBody = JsonConvert.SerializeObject(payload);
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
            string userName = Request.Headers[EasyAuthUserHeader].FirstOrDefault();
            ViewData["UserName"] = string.IsNullOrEmpty(userName) ? "Unknown User" : userName;
            ViewData["Message"] = message;
            return View();
        }

        public IActionResult Failure(string message)
        {
            string userName = Request.Headers[EasyAuthUserHeader].FirstOrDefault();
            ViewData["UserName"] = string.IsNullOrEmpty(userName) ? "Unknown User" : userName;
            ViewData["Message"] = message;
            return View();
        }
    }

    public class ActionRequest
    {
        public string ActionName { get; set; }
        public bool IsApproved { get; set; }
        public string ApproverName { get; set; }
    }
}
