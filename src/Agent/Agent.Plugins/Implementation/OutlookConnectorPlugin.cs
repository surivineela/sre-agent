// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

public class OutlookConnectorPlugin : IOutlookConnectorPlugin
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IConnectorResolver _connectorResolver;
    private readonly IAuthenticationService _authenticationService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OutlookConnectorPlugin> _logger;

    public OutlookConnectorPlugin(
        ILogger<OutlookConnectorPlugin> logger,
        IConnectorResolver connectorResolver,
        IHttpClientFactory httpClientFactory,
        IAuthenticationService authenticationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectorResolver = connectorResolver ?? throw new ArgumentNullException(nameof(connectorResolver));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    }

    public async Task<EmailSendResult> SendEmailAsync(
        string to,
        string subject,
        string body,
        string bodyType,
        string importance,
        string? cc,
        string? bcc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(body);

        string trimmedTo = to.Trim();
        string trimmedSubject = subject.Trim();
        string trimmedBody = body.Trim();

        if (string.IsNullOrWhiteSpace(trimmedTo) || string.IsNullOrWhiteSpace(trimmedSubject) || string.IsNullOrWhiteSpace(trimmedBody))
        {
            return new EmailSendResult
            {
                Success = false,
                StatusCode = 400,
                ResponseContent = string.Empty,
                Message = "Recipient, subject, and body must all be provided."
            };
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var connector = _connectorResolver.GetConnectorFromSettings<OutlookConnector>(
                connectorName: string.Empty,
                connectorType: "Outlook",
                dataSource: string.Empty);

            var credential = _authenticationService.GetDataConnectorCredential(connector.Auth);
            var tokenRequest = new TokenRequestContext(new[] { "https://management.core.windows.net/" });
            var accessToken = await credential.GetTokenAsync(tokenRequest, cancellationToken);

            if (string.IsNullOrWhiteSpace(accessToken.Token))
            {
                _logger.LogInternalError("Failed to acquire access token for Outlook connector.");
                return new EmailSendResult
                {
                    Success = false,
                    StatusCode = 401,
                    ResponseContent = string.Empty,
                    Message = "Failed to acquire access token for email connector."
                };
            }

            var client = _httpClientFactory.CreateClient();
            var baseUrl = EnsureTrailingSlash(connector.ConnectionRuntimeUrl);
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

            var payload = new SendEmailPayload
            {
                To = trimmedTo,
                Subject = trimmedSubject,
                Body = trimmedBody,
                Importance = NormalizeImportance(importance),
                IsHtml = !string.Equals(bodyType, "text", StringComparison.OrdinalIgnoreCase),
                Cc = string.IsNullOrWhiteSpace(cc) ? null : cc,
                Bcc = string.IsNullOrWhiteSpace(bcc) ? null : bcc
            };

            var content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

            _logger.LogInternalInformation("Sending email via Outlook connector endpoint {Endpoint}", baseUrl);

            using var response = await client.PostAsync("v2/Mail", content, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            bool success = response.IsSuccessStatusCode;
            if (success)
            {
                _logger.LogInternalInformation("Email sent successfully with status code {StatusCode}", (int)response.StatusCode);
            }
            else
            {
                _logger.LogInternalWarning("Email send failed with status code {StatusCode} and response {Response}", (int)response.StatusCode, responseBody);
            }

            return new EmailSendResult
            {
                Success = success,
                StatusCode = (int)response.StatusCode,
                ResponseContent = responseBody,
                Message = success ? "Email sent successfully." : "Email send request failed."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Unexpected error while sending email through Outlook connector.");
            return new EmailSendResult
            {
                Success = false,
                StatusCode = 500,
                ResponseContent = string.Empty,
                Message = "Unexpected error while sending email."
            };
        }
    }

    private static string EnsureTrailingSlash(string url)
    {
        return url.EndsWith('/') ? url : url + '/';
    }

    private static string NormalizeImportance(string importance)
    {
        if (string.IsNullOrWhiteSpace(importance))
        {
            return "Normal";
        }

        return importance.Trim().ToLowerInvariant() switch
        {
            "low" => "Low",
            "high" => "High",
            _ => "Normal"
        };
    }

    private sealed class SendEmailPayload
    {
        public string To { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Importance { get; init; } = "Normal";
        public bool IsHtml { get; init; }
        public string? Cc { get; init; }
        public string? Bcc { get; init; }
    }
}
