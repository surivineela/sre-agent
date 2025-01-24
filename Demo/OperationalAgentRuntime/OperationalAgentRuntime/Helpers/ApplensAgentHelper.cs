using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using OperationalAgentRuntime.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Helpers
{
    public static class ApplensAgentHelper
    {
        private static readonly TokenCredential credential;
        private static readonly HttpClient httpClient;

        static ApplensAgentHelper()
        {
            var environment = Environment.GetEnvironmentVariable("Environment") ?? "Development";

            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                credential = new ManagedIdentityCredential();
            }
            else
            {
                credential = new DefaultAzureCredential();
            }

            httpClient = new HttpClient();
        }

        public static async Task<ApplensIssueRootCause> GetProblemRootCause(string armResourceId, string problemStatement)
        {
            await Task.Delay(10000);
            // TODO : Need tp call into Applens conversational diagnostics agent
            return new ApplensIssueRootCause()
            {
                RootCauseIntent = "memory",
                RootCauseMessage = "High Memory usage may be the cause of application downtime",
                QuickMitigation = new QuickMitigation[] { QuickMitigation.Reboot, QuickMitigation.ScaleUp },
                DataCollection = DataCollection.MemoryDump
            };
        }

        public static async Task<Tuple<string, string, string>> GetApplensBotTokenAsync(
            string armResourceId, 
            string accessToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(accessToken);
            var tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value ?? "unknownTenantId";
            var puid = jwtToken.Claims.FirstOrDefault(c => c.Type == "puid")?.Value ?? "unknownPuid";
            var userId = $"dl_{tenantId}_{puid}";
            string sessionId, botToken;

            // Build the ARM GET endpoint
            var armUrl = $"https://management.azure.com/{armResourceId}/detectors/GetToken-db48586f-7d94-45fc-88ad-b30ccd3b571c?api-version=2015-08-01";

            var armRequest = new HttpRequestMessage(HttpMethod.Get, armUrl);
            armRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var armResponse = await httpClient.SendAsync(armRequest);
            armResponse.EnsureSuccessStatusCode();
            var armResponseString = await armResponse.Content.ReadAsStringAsync();

            using (JsonDocument doc = JsonDocument.Parse(armResponseString))
            {
                var root = doc.RootElement;
                var properties = root.GetProperty("properties");
                var dataset = properties.GetProperty("dataset")[0];
                var table = dataset.GetProperty("table");
                var rows = table.GetProperty("rows")[0];

                // rows is in the form: [ "<SESSION_ID>", "<BOT_TOKEN>", <EXPIRES_IN_SECONDS> ]
                sessionId = rows[0].GetString();
                botToken = rows[1].GetString();
            }
            return new Tuple<string, string, string>(userId, sessionId, botToken);
        }

        public static async Task<string> SendDiagnosticsMessageAsync(
                string armResourceId,
                Tuple<string, string, string> userAuthData,
                string userMessage,
                bool isClearChat)
        {

            var diagUrl = "https://diagnosticschat.azure.com/api/ConversationalDiagForAzCopilot/SendMessage";

            var diagRequest = new HttpRequestMessage(HttpMethod.Post, diagUrl);
            diagRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuthData.Item3);

            // For non-portal scenarios, set an allowed Origin header
            diagRequest.Headers.Add("Origin", "https://appservice-diagnostics.trafficmanager.net");

            // Build the body; adjust start/end time, resourceKind, etc. to your scenario
            var requestBody = new
            {
                sessionId = userAuthData.Item2,
                resourceId = armResourceId,
                resourceKind = "app",
                userId = userAuthData.Item1,
                message = userMessage,
                isClearChatRequest = isClearChat,
                startTime = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"),
                endTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);
            diagRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var diagResponse = await httpClient.SendAsync(diagRequest);
            diagResponse.EnsureSuccessStatusCode();

            // Return the raw JSON from the API's response
            var diagResponseString = await diagResponse.Content.ReadAsStringAsync();
            return diagResponseString;
        }
    }
}
