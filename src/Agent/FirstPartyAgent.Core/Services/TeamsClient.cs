// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
        Task<bool> PostMessageOnTeams(string agentMode, TeamsMessage message);
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

        public async Task<bool> PostMessageOnTeams(string agentMode, TeamsMessage message)
        {
            var httpClient = new HttpClient();
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var conversationId = _teamsClientSettings.AgentConversationIds.ContainsKey(agentMode) ? _teamsClientSettings.AgentConversationIds[agentMode] : _teamsClientSettings.TeamsGroupConversationId;

            var payload = new
            {
                conversationId,
                message
            };

            var requestBody = JsonSerializer.Serialize(payload, jsonOptions);
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

