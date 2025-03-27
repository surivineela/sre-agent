using Agent.Core.Models;
using FirstPartyAgent.Core.Configuration;
using System.Text;
using System.Text.Json;

namespace FirstPartyAgent.Core.Services
{
    public interface ITeamsClient
    {
        bool IsEnabled();
        bool SendLogsToTeams();
        Task<bool> PostMessageOnTeams(string textContent, string base64Image = null);
    }

    public class TeamsClient: ITeamsClient
    {
        private static HttpClient _httpClient;
        private readonly string teamsChatEndpoint;
        private readonly string teamsGroupConversationId;
        private readonly bool sendLogsToTeams;
        public TeamsClient(TeamsClientSettings teamsClientSettings)
        {
            _httpClient = new HttpClient();
            sendLogsToTeams = teamsClientSettings.SendLogsToTeams;
            teamsChatEndpoint = teamsClientSettings.TeamsEndpoint;
            teamsGroupConversationId = teamsClientSettings.TeamsGroupConversationId;
        }

        public bool SendLogsToTeams()
        {
            return sendLogsToTeams;
        }

        public bool IsEnabled()
        {
            return !string.IsNullOrWhiteSpace(teamsChatEndpoint);
        }

        public async Task<bool> PostMessageOnTeams(string textContent, string base64Image=null)
        {
            var httpClient = new HttpClient();
            var teamsMessage = new TeamsMessage(textContent, base64Image);
            var payload = new
            {
                conversationId = teamsGroupConversationId,
                message = teamsMessage
            };

            var options1 = new JsonSerializerOptions { WriteIndented = true };
            var requestBody = JsonSerializer.Serialize(payload, options1);
            var response = await httpClient.PostAsync(teamsChatEndpoint, new StringContent(requestBody, Encoding.UTF8, "application/json"));
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
