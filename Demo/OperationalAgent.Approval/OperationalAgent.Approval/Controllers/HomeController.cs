using System.Diagnostics;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using OperationalAgent.Approval.Models;

namespace OperationalAgent.Approval.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _memoryCache;
        private const string EasyAuthUserHeader = "X-MS-CLIENT-PRINCIPAL-NAME";
        private const string ApprovalsCacheKey = "Approvals";

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, HttpClient httpClient, IMemoryCache memoryCache)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = configuration;
            _memoryCache = memoryCache;
        }

        // See Program.cs and README for the difference between these two routes
        public IActionResult Index(string action_name)
        {
            string userName = Request.Headers[EasyAuthUserHeader].FirstOrDefault();
            ViewData["UserName"] = string.IsNullOrEmpty(userName) ? "Unknown User" : userName;
            ViewData["ActionName"] = action_name;
            return View();
        }

        public IActionResult RequestApproval(string data)
        {
            string userName = Request.Headers[EasyAuthUserHeader].FirstOrDefault();
            ViewData["UserName"] = string.IsNullOrEmpty(userName) ? "Unknown User" : userName;

            if (!string.IsNullOrEmpty(data))
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                var approvalRequest = JsonConvert.DeserializeObject<ApprovalRequest>(json);

                if (approvalRequest != null)
                {
                    ViewData["ApprovalId"] = approvalRequest.ApprovalId;
                    ViewData["ActionName"] = approvalRequest.Reason;
                    ViewData["Description"] = approvalRequest.Description;
                    ViewData["CallbackUrl"] = approvalRequest.CallbackUrl;
                    return View();
                }
            }

            return View();
        }

        // This method is used in the second flow that invokes an SK function.
        // It also stores approvals in memory that can be polled at /GetApprovals (unused)
        // The ?action_name query param should be any arbitrary ID to identify your approval request
        [HttpPost("ProcessApprovalDecision")]
        public async Task<IActionResult> ProcessApprovalDecision([FromBody] ActionRequest request)
        {
            string nextPageMessage = string.Empty;
            string eventName = string.Empty;

            //http://localhost:5073/api/v1/approvals/approval-f5751a55-bafa-5a49-918e-3ce82edb685d/decision { "status": "Approved" }.

            var payload = new ApprovalDecisionRequest()
            {
                Status = request.IsApproved ? "Approved" : "Rejected",
                User = request.ApproverName
            };

            try
            {
                // This posts to the SK function for durable function entrypoint
                await this._httpClient.PostAsJsonAsync($"{request.CallbackUrl}/api/v1/approvals/{request.ApprovalId}/decision", payload);

                // Store approval payload in memory cache
                var approvals = await _memoryCache.GetOrCreateAsync(ApprovalsCacheKey, entry =>
                {
                    return Task.FromResult(new List<ApprovalDecisionRequest>());
                });

                approvals.Add(payload);
                _memoryCache.Set(ApprovalsCacheKey, approvals);
            }
            catch
            {
                return BadRequest(new { redirectUrl = Url.Action("failure", new { message = "Failed to store approval decision." }) });
            }

            var summaryMessage = $"Processed decision by {payload.User} for operation {request.ApprovalId}";
            return Ok(new { redirectUrl = Url.Action(request.IsApproved ? "success" : "failure", new { message = summaryMessage }) });
        }

        [HttpGet("GetApprovals")]
        public async Task<IActionResult> GetApprovals()
        {
            var approvals = await _memoryCache.GetOrCreateAsync(ApprovalsCacheKey, entry =>
            {
                return Task.FromResult(new List<ApprovalPayload>());
            });

            return Ok(approvals);
        }

        // This method is used in the original flow (we post event to waiting durable function)
        // The ?action_name parameter should be one of the explicit action names below
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
            else if (string.Equals(request.ActionName, "MonitorAvailability_instance", StringComparison.OrdinalIgnoreCase))
            {
                isValid = true;
                eventName = "ApproveMemoryDumpAndScaleUp";
            }
            else if (string.Equals(request.ActionName, "ApproveTLSUpdate_instance", StringComparison.OrdinalIgnoreCase))
            {
                isValid = true;
                eventName = "ApproveUpdateMinimumTLSEvent";
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
                string nextPage = approvalSuccess ? "Success" : "Failure";

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

    public class ApprovalRequest
    {
        public string ApprovalId { get; set; }
        public string Reason { get; set; }
        public string Description { get; set; }
        public string CallbackUrl { get; set; }
    }

    public class ApprovalPayload
    {
        public string Id { get; set; }
        public bool IsApproved { get; set; }
        public string ApproverName { get; set; }
        public string CallbackUrl { get; set; }
    }

    public class ApprovalDecisionRequest
    {
        public string Status { get; set; }
        public string User { get; set; }
    }

    public class ActionRequest
    {
        public string ApprovalId { get; set; }
        public string ActionName { get; set; }
        public bool IsApproved { get; set; }
        public string ApproverName { get; set; }
        public string Description { get; set; }
        public string CallbackUrl { get; set; }
    }
}
