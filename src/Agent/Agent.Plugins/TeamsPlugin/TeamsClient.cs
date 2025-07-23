// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Models;

namespace Agent.Plugins.TeamsPlugin
{
    public interface ITeamsClient
    {
        bool IsEnabled();
        bool SendLogsToTeams();
        Task<bool> PostMessageOnTeams(string agentMode, TeamsMessage message);
        Task<double> CreateTeamsChannelPost(TeamsMessage message);
    }

    public class TeamsClient : ITeamsClient
    {
        private static HttpClient _httpClient = new HttpClient();
        private readonly TeamsClientSettings _teamsClientSettings;
        public TeamsClient(TeamsClientSettings teamsClientSettings)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(240);
            _teamsClientSettings = teamsClientSettings;
        }

        public bool SendLogsToTeams()
        {
            return _teamsClientSettings.SendLogsToTeams;
        }

        public bool IsEnabled()
        {
            return _teamsClientSettings.Enabled && !(string.IsNullOrWhiteSpace(_teamsClientSettings.TeamsEndpoint)
                && string.IsNullOrWhiteSpace(_teamsClientSettings.CreateTeamsChannelPostUrl));
        }

        private async Task<HttpResponseMessage> SendMessageAsync(string endpoint, string requestBody)
        {
            var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(endpoint, new StringContent(requestBody, Encoding.UTF8, "application/json"));
            return response;
        }

        //Returns the post Id
        public async Task<double> CreateTeamsChannelPost(TeamsMessage message)
        {
            message.GroupId = _teamsClientSettings.GroupId;
            message.ChannelId = _teamsClientSettings.ChannelId;
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

            var payload = new
            {
                message
            };

            var requestBody = JsonSerializer.Serialize(payload, jsonOptions);
            if (string.IsNullOrWhiteSpace(_teamsClientSettings.CreateTeamsChannelPostUrl))
            {
                throw new Exception($"CreateTeamsChannelPostUrl is null or empty");
            }
            var response = await SendMessageAsync(_teamsClientSettings.CreateTeamsChannelPostUrl, requestBody);
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                if (double.TryParse(responseBody, out var postId))
                {
                    return postId;
                }
                else
                {
                    throw new Exception($"Failed to parse post ID from response: {responseBody}");
                }
            }
            else
            {
                throw new Exception($"Failed to create Teams post. Status code: {response.StatusCode}");
            }
        }

        private async Task<HttpResponseMessage> SendChannelMessage(TeamsMessage message)
        {
            message.GroupId = _teamsClientSettings.GroupId;
            message.ChannelId = _teamsClientSettings.ChannelId;
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

            var payload = new
            {
                message
            };

            var requestBody = JsonSerializer.Serialize(payload, jsonOptions);
            if (string.IsNullOrWhiteSpace(_teamsClientSettings.ReplyToTeamsChannelPostUrl))
            {
                throw new Exception($"ReplyToTeamsChannelPostUrl is null or empty");
            }
            var response = await SendMessageAsync(_teamsClientSettings.ReplyToTeamsChannelPostUrl, requestBody);
            return response;
        }

        private async Task<HttpResponseMessage> SendChatMessage(string agentMode, TeamsMessage message)
        {
            var conversationId = _teamsClientSettings.AgentConversationIds.ContainsKey(agentMode) ? _teamsClientSettings.AgentConversationIds[agentMode] : _teamsClientSettings.TeamsGroupConversationId;
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

            var payload = new
            {
                conversationId,
                message
            };

            var requestBody = JsonSerializer.Serialize(payload, jsonOptions);
            if (string.IsNullOrWhiteSpace(_teamsClientSettings.TeamsEndpoint))
            {
                throw new Exception($"TeamsEndpoint is null or empty");
            }
            var response = await SendMessageAsync(_teamsClientSettings.TeamsEndpoint, JsonSerializer.Serialize(payload));
            return response;
        }

        public async Task<bool> PostMessageOnTeams(string agentMode, TeamsMessage message)
        {
            var httpClient = new HttpClient();
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            HttpResponseMessage response = new HttpResponseMessage();

            if (_teamsClientSettings.UseTeamsChannel)
            {
                response = await SendChannelMessage(message);
            }
            else
            {
                response = await SendChatMessage(agentMode, message);
            }

            if (response != null && response.IsSuccessStatusCode)
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

