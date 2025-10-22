// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AdaptiveCards;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Extensions;
using Agent.Plugins.Interface;
using Agent.Plugins.Tools;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class TeamsPlugin : ITeamsPlugin
    {
        private readonly ILogger<TeamsPlugin> _logger;
        private static readonly JsonSerializerOptions _jsonOptions = new (JsonSerializerOptions.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        private readonly IConnectorResolver _connectorResolver;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthenticationService _authenticationService;
        private readonly TeamsApiHubConnector _teamsApiHubConnector;
        private string _apiHubRuntimeUrl;

        public TeamsPlugin(
            ILogger<TeamsPlugin> logger,
            IConnectorResolver connectorResolver,
            IHttpClientFactory httpClientFactory,
            IAuthenticationService authenticationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectorResolver = connectorResolver ?? throw new ArgumentNullException(nameof(connectorResolver));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _teamsApiHubConnector = _connectorResolver.GetConnectorFromSettings<TeamsApiHubConnector>(
                connectorName: string.Empty,
                connectorType: "Teams",
                dataSource: string.Empty);

            // Ensure the base URL ends with a slash, because HttpClient.BaseAddress requires it
            _apiHubRuntimeUrl = _teamsApiHubConnector.ConnectionRuntimeUrl.EndsWith('/') ? _teamsApiHubConnector.ConnectionRuntimeUrl : _teamsApiHubConnector.ConnectionRuntimeUrl + '/';

        }

        /// <summary>
        /// Send a message to a specific Teams channel
        /// </summary>
        /// <param name="message">message in html</param>
        public async Task<PostMessageResult> PostMessageToChannel(string subject, string message, CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation($"[PostMessageToChannel] Sending message to Teams channel. Url: {_teamsApiHubConnector.ConnectionRuntimeUrl}, GroupId: {_teamsApiHubConnector.GroupId}, ChannelId: {_teamsApiHubConnector.ChannelId}");
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogInternalWarning("[PostMessageToChannel] Message content is empty. Aborting send.");
                throw new ArgumentException("message should not be empty");
            }

            var accessToken = await GetAccessTokenAsync(cancellationToken);
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_apiHubRuntimeUrl, UriKind.Absolute);
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var payload = new
            {
                subject,
                body = new
                {
                    contentType = "html",
                    content = message
                }
            };

            using var response = await httpClient.PostAsJsonAsync($"v3/beta/teams/{_teamsApiHubConnector.GroupId}/channels/{_teamsApiHubConnector.ChannelId}/messages", payload, _jsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInternalError($"[PostMessageToChannel] Failed to send message to Teams channel. StatusCode: {response.StatusCode}, Response: {errorContent}");
                throw new HttpRequestException($"Failed to send message to Teams channel. StatusCode: {response.StatusCode}, Response: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var postedMessage = JsonSerializer.Deserialize<TeamsChannelMessage>(responseContent, _jsonOptions);

            if (postedMessage == null)
            {
                _logger.LogInternalError("[PostMessageToChannel] Failed to deserialize response from Teams API.");
                throw new InvalidOperationException("Failed to deserialize response from Teams API.");
            }

            _logger.LogInternalInformation($"[PostMessageToChannel] Message sent successfully. MessageId: {postedMessage.Id}, WebUrl: {postedMessage.WebUrl}");

            return new PostMessageResult(postedMessage.Id, postedMessage.WebUrl);
        }

        /// <summary>
        /// Get messages from a specific Teams channel
        /// </summary>
        /// <returns>The most recent message from the channel</returns>
        public async Task<List<TeamsChannelMessage>> GetMessagesFromChannel(CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation($"[GetMessagesFromChannel] Getting messages from Teams channel. Url: {_teamsApiHubConnector.ConnectionRuntimeUrl}, GroupId: {_teamsApiHubConnector.GroupId}, ChannelId: {_teamsApiHubConnector.ChannelId}");

            var accessToken = await GetAccessTokenAsync(cancellationToken);
            _logger.LogInternalInformation($"[GetMessagesFromChannel] Successfully obtained access token.");
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_apiHubRuntimeUrl, UriKind.Absolute);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.GetAsync($"beta/teams/{_teamsApiHubConnector.GroupId}/channels/{_teamsApiHubConnector.ChannelId}/messages", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInternalError($"[GetMessagesFromChannel] Failed to get messages from Teams channel. StatusCode: {response.StatusCode}, Response: {errorContent}");
                throw new HttpRequestException($"Failed to get messages from Teams channel. StatusCode: {response.StatusCode}, Response: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var messagesResponse = JsonSerializer.Deserialize<TeamsMessagesResponse>(responseContent, _jsonOptions);

            if (messagesResponse?.Value == null || messagesResponse.Value.Count == 0)
            {
                _logger.LogInternalWarning("[GetMessagesFromChannel] No messages found in the channel.");
                return [];
            }

            var messages = messagesResponse.Value;
            _logger.LogInternalInformation($"[GetMessagesFromChannel] Retrieved {messages.Count} messages successfully.");

            return messages;
        }

        private record TeamsMessagesResponse(List<TeamsChannelMessage> Value);

        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var credential = _authenticationService.GetDataConnectorCredential(_teamsApiHubConnector.Auth);
            var tokenRequest = new TokenRequestContext(new[] { "https://management.core.windows.net/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequest, cancellationToken);
            return accessToken.Token;
        }
    }
}
