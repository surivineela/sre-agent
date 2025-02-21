using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstPartyAgent.Helpers;

namespace FirstPartyAgent.Plugins
{
    public class ICMFunctionAppPlugin
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private const int timeoutInSeconds = 600;
        // When ReadOnly is true, we will skip running the tools that perform write actions
        private readonly bool ReadOnly;

        public ICMFunctionAppPlugin(IConfiguration configuration)
        {
            _config = configuration;
            var functionAppKey = _config.GetValue("ICM:PluginAppKey", string.Empty);
            ReadOnly = _config.GetValue("ICM:ReadOnly", false);
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-functions-key", functionAppKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutInSeconds);
        }

        private async Task<string> ProcessComplexICMContent(string complexContent, Kernel kernel)
        {
            List<(string, string)> base64Images = new List<(string, string)>();
            if (complexContent != null)
            {
                // remove base64 images from the complexContent (they would blow the response which goes back to the model and the model wouldn't make sense out of it) and store them in a list
                complexContent = TextProcessingHelpers.StripBase64Images(complexContent, base64Images);

                // remove html attributes as they don't provide much value and make the response longer (todo: it might be useful to strip html tags completely and convert the text rather to markdown or something like that)
                complexContent = TextProcessingHelpers.RemoveHtmlAttributes(complexContent);

                var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

                for (int i = 0; i < base64Images.Count; i++)
                {
                    // extract text from the image and replace the placeholder in the summary with the extracted text
                    ChatMessageContent result = await ExtractTextFromImage(kernel, base64Images[i].Item1, base64Images[i].Item2, chatCompletionService);

                    complexContent = complexContent.Replace($"####{i}####", "[The following text was in an image in the incident]" + result.Content + "\r\n[End of the image]");
                }
            }
            return complexContent;
        }

        private async Task<HttpResponseMessage> SendRequestWithRetry(HttpRequestMessage requestMessage, bool retry = true)
        {
            var cts = new CancellationTokenSource();
            try
            {
                return await _httpClient.SendAsync(requestMessage, cts.Token);
            }
            catch (TaskCanceledException ex)
            {
                if (ex.CancellationToken == cts.Token && retry)
                {
                    return await SendRequestWithRetry(requestMessage, false);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                if (retry)
                {
                    return await SendRequestWithRetry(requestMessage, false);
                }
                else
                {
                    throw ex;
                }
            }
        }

        private async Task<HttpResponseMessage> ExecuteICMWorkflow(string workflowName, string payload)
        {
            string PluginUrl = _config.GetValue("ICM:PluginUrl", string.Empty);
            if (string.IsNullOrWhiteSpace(PluginUrl))
            {
                throw new Exception("ICM:PluginUrl is not set in the configuration");
            }
            Dictionary<string, string> body = new Dictionary<string, string>();
            body.Add("workflowName", workflowName);
            body.Add("body", payload);
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, PluginUrl);
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            return await SendRequestWithRetry(requestMessage, true);
        }

        [KernelFunction("get_icm_incident_details")]
        [Description("Get ICM incident details")]
        public async Task<Incident> GetIncidentInfo(
           [Description("Incident ID")] string incidentId, Kernel kernel)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
            var response = await ExecuteICMWorkflow("GetIncidentInfo-AppLensAutomation", payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                var incident = JsonConvert.DeserializeObject<Incident>(content);

                incident.Summary = await ProcessComplexICMContent(incident.Summary, kernel);

                return incident;
            }
            else
            {
                Console.WriteLine($"Failed to fetch incident info for incidentId: {incidentId}");
                return null;
            }
        }

        /// <summary>
        /// Extracts text from an image using the chat completion service with SK (does not carry around the conversation history/context etc.; no tool calling)
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="mimeType"></param>
        /// <param name="base64Image"></param>
        /// <param name="chatCompletionService"></param>
        /// <returns></returns>
        private static async Task<ChatMessageContent> ExtractTextFromImage(Kernel kernel, string mimeType, string base64Image, IChatCompletionService chatCompletionService)
        {
            Console.WriteLine($"Extracting text from image ({mimeType}, {base64Image.Length} characters)");

            var history = new ChatHistory();
            var message = new ChatMessageContentItemCollection
                        {
                            new TextContent("Please extract the text from the image"),
                            new ImageContent($"{mimeType};base64,{base64Image}")
                        };

            history.AddUserMessage(message);

            var result = await chatCompletionService.GetChatMessageContentAsync(
            history,
            executionSettings: new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None()
            },
            kernel: kernel);

            return result;
        }

        [KernelFunction("get_icm_discussion_entries")]
        [Description("Get ICM discussion entries")]
        public async Task<List<DiscussionEntry>> GetDiscussionEntries(
            [Description("Incident ID")] string incidentId, Kernel kernel)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
            var response = await ExecuteICMWorkflow("GetDiscussionEntries-SREAgent-1P-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var discussionEntries = JsonConvert.DeserializeObject<List<DiscussionEntry>>(content);
                foreach (var entry in discussionEntries)
                {
                    if (entry.IsHtml)
                    {
                        entry.Text = await ProcessComplexICMContent(entry.Text, kernel);
                    }
                }
                return discussionEntries;
            }
            else
            {
                Console.WriteLine($"Failed to fetch discussion entries for incidentId: {incidentId}");
                return null;
            }
        }

        [KernelFunction("get_applens_diagnostics_for_icm_incident")]
        [Description("Get AppLens diagnostics for ICM incident")]
        public async Task<string> GetAppLensDiagnostics(
           [Description("Incident ID")] string incidentId)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
            var response = await ExecuteICMWorkflow("ApplensPlugin-SREAgent-1P-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                Console.WriteLine($"Failed to fetch AppLens diagnostics for incidentId: {incidentId}");
                return $"Failed to fetch AppLens diagnostics for incidentId: {incidentId}";
            }
        }

        [KernelFunction("transfer_icm_incident")]
        [Description("Transfer ICM incident")]
        public async Task<string> TransferIncident(
               [Description("Incident ID")] string incidentId,
               [Description("Discussion Entry - reason for transferring the incident")] string discussionEntry,
               [Description("Tenant of the team to transfer the incident to")] string tenantName,
               [Description("Owning Team to transfer the incident to")] string owningTeam)
        {
            if (ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry, tenantName, owningTeam });
            var response = await ExecuteICMWorkflow("TransferIncident-SREAgent-1P-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to transfer incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        [KernelFunction("mitigate_icm_incident")]
        [Description("Mitigate ICM incident")]
        public async Task<string> MitigateIncident(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry - reason for mitigating the incident")] string discussionEntry)
        {
            if (ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
            var response = await ExecuteICMWorkflow("MitigateIncident-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to mitigate incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        [KernelFunction("downgrade_sev2_incident_to_sev3")]
        [Description("Downgrade severity of ICM incident 2 to 3")]
        public async Task<string> DowngradeSeverity(
            [Description("Incident ID")] string incidentId,
            [Description("Discussion Entry - reason for downgrading the incident")] string discussionEntry)
        {
            if (ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
            var response = await ExecuteICMWorkflow("DowngradeSev2-SREAgent-1P-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to downgrade severity of incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        [KernelFunction("transfer_icm_incident_to_human_intervention")]
        [Description("Transfer ICM incident to human intervention")]
        public async Task<string> TransferIncidentToHumanIntervention(
            [Description("Incident ID")] string incidentId,
            [Description("Discussion Entry - reason for transferring the incident to human intervention")] string discussionEntry)
        {
            if (ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }

            return await TransferIncident(incidentId, discussionEntry, "HUMAN_INTERVENTION", "HUMAN_INTERVENTION");
        }

        [KernelFunction("resolve_icm_incident")]
        [Description("Resolve ICM incident")]
        public async Task<string> ResolveIncident(
               [Description("Incident ID")] string incidentId,
               [Description("Discussion Entry - reason for resolving the incident")] string discussionEntry)
        {
            if (ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
            var response = await ExecuteICMWorkflow("ResolveIncident-SREAgent-1P-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to resolve incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        [KernelFunction("post_icm_discussion_entry")]
        [Description("Post ICM discussion entry")]
        public async Task<string> PostDiscussionEntry(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry")] string discussionEntry)
        {
            if (ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
            var response = await ExecuteICMWorkflow("PostDiscussionEntry-SREAgent-1P-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to post discussion entry for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        [KernelFunction("mark_subscription_as_first_party")]
        [Description("Mark Subscription as first party")]
        public async Task<string> MarkSubFirstParty(
           [Description("Subscription ID")] string subscriptionId)
        {
            if (ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { subscriptionId });
            var response = await ExecuteICMWorkflow("MFPSub-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                string errorMessage = $"Failed to mark subscription as first party for subscriptionId: {subscriptionId}";
                Console.WriteLine(errorMessage);
                return errorMessage;
            }
        }

        [KernelFunction("get_subscription_details_from_geneva")]
        [Description("Get subscription details from geneva")]
        public async Task<string> GetSubDetailsFromGeneva(
           [Description("Subscription ID")] string subscriptionId)
        {
            var payload = JsonConvert.SerializeObject(new { subscriptionId });
            var response = await ExecuteICMWorkflow("GetSubGeneva-AJSHARM", payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                Console.WriteLine($"Failed to fetch sub details for subscriptionId: {subscriptionId}");
                return $"Failed to fetch sub details for subscriptionId: {subscriptionId}";
            }
        }

        [KernelFunction("get_icm_incidents_by_team")]
        [Description("Gets a list of ICM incidents by Tenant and Team")]
        public async Task<List<Incident>> GetIncidents(
        [Description("The name of the tenant")] string tenant,
        [Description("Comma-separated list of metrics to include")] string metrics)
        {
            return new List<Incident>();
        }
    }
}
