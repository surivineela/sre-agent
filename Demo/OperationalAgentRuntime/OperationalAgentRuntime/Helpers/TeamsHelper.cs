using Newtonsoft.Json;
using OperationalAgentRuntime.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Helpers
{
    public static class TeamsHelper
    {
        private static readonly HttpClient HttpClient;
        private static readonly string? TeamsPostMessageEndpoint;

        static TeamsHelper()
        {
            HttpClient = new HttpClient();
            TeamsPostMessageEndpoint = Environment.GetEnvironmentVariable("TeamsPostMessageEndpoint");
        }

        public static async Task<bool> PostMessageAsync(TeamsMessage teamsMessage)
        {
            if (teamsMessage == null || string.IsNullOrWhiteSpace(teamsMessage.Content)) return false;

            var payload = new
            {
                message = teamsMessage
            };

            var requestBody = JsonConvert.SerializeObject(payload, Formatting.Indented);
            var response = await HttpClient.PostAsync(TeamsPostMessageEndpoint, new StringContent(requestBody, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                throw new Exception($"Teams Post Message Call Failed. Status Code : {response.StatusCode}, Error : {await response.Content.ReadAsStringAsync()}");
            }
        }
    }
}
