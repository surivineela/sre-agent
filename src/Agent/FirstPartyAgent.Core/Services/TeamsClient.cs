using Agent.Core.Models;
using FirstPartyAgent.Core.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace FirstPartyAgent.Core.Services
{
    public interface ITeamsClient
    {
        bool IsEnabled();
        bool SendLogsToTeams();
        Task<bool> PostMessageOnTeams(string textContent, string agentMode, string base64Image = null);
    }

    public class TeamsClient: ITeamsClient
    {
        private static HttpClient _httpClient;
        private readonly TeamsClientSettings _teamsClientSettings;
        public TeamsClient(TeamsClientSettings teamsClientSettings)
        {
            _httpClient = new HttpClient();
            _teamsClientSettings = teamsClientSettings;
        }

        public bool SendLogsToTeams()
        {
            return _teamsClientSettings.SendLogsToTeams;
        }

        public bool IsEnabled()
        {
            return !string.IsNullOrWhiteSpace(_teamsClientSettings.TeamsEndpoint);
        }

        public async Task<bool> PostMessageOnTeams(string textContent, string agentMode, string base64Image=null)
        {
            var httpClient = new HttpClient();
            var teamsMessage = new TeamsMessage(textContent, base64Image);
            var conversationId = _teamsClientSettings.AgentConversationIds.ContainsKey(agentMode) ? _teamsClientSettings.AgentConversationIds[agentMode] : _teamsClientSettings.TeamsGroupConversationId;
            var payload = new
            {
                conversationId = conversationId,
                message = teamsMessage
            };

            var options1 = new JsonSerializerOptions { WriteIndented = true };
            var requestBody = JsonSerializer.Serialize(payload, options1);
            var response = await httpClient.PostAsync(_teamsClientSettings.TeamsEndpoint, new StringContent(requestBody, Encoding.UTF8, "application/json"));
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
