using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OperationalAgentRuntime.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Helpers
{
    public static class OpenAIHelper
    {
        private static readonly HttpClient HttpClient;
        private static readonly string? OpenAIAPI_Endpoint;
        private static readonly string? OpenAIAPI_KEY;

        static OpenAIHelper()
        {
            HttpClient = new HttpClient();
            OpenAIAPI_Endpoint = Environment.GetEnvironmentVariable("OpenAIEndpoint");
            OpenAIAPI_KEY = Environment.GetEnvironmentVariable("OpenAIAPI_KEY");
            HttpClient.DefaultRequestHeaders.Add("api-key", OpenAIAPI_KEY);
        }

        public static async Task<string> GetOpenAIResponseAsync(List<OpenAIMessage> messages)
        {
            if (messages == null || messages.Count == 0) return string.Empty;

            var payload = new
            {
                messages,
                temperature = 0.3,
                top_p = 0.95,
                max_tokens = 800,
                stream = false
            };

            var requestBody = JsonConvert.SerializeObject(payload, Formatting.Indented);
            var response = await HttpClient.PostAsync(OpenAIAPI_Endpoint, new StringContent(requestBody, Encoding.UTF8, "application/json"));
            if (response.IsSuccessStatusCode)
            {
                var responseData = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                return responseData?["choices"][0]["message"]["content"] ?? string.Empty;
            }
            else
            {
                throw new Exception($"Open AI Call Failed. Status Code : {response.StatusCode}, Error : {await response.Content.ReadAsStringAsync()}");
            }
        }
    }
}
