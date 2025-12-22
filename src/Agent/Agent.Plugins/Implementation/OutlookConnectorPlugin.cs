// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
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

    private const int MaxEmailListPageSize = 50;
    private static readonly string[] DefaultConnectorScopes = new[] { "https://management.core.windows.net/" };

    private readonly IAuthenticationService _authenticationService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OutlookConnectorPlugin> _logger;
    private readonly Lazy<OutlookConnector> _connector;

    public OutlookConnectorPlugin(
        ILogger<OutlookConnectorPlugin> logger,
        IConnectorResolver connectorResolver,
        IHttpClientFactory httpClientFactory,
        IAuthenticationService authenticationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(connectorResolver);
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _connector = new Lazy<OutlookConnector>(() => connectorResolver.GetConnectorFromSettings<OutlookConnector>(
            connectorName: string.Empty,
            connectorType: "Outlook",
            dataSource: string.Empty));
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
            var clientResult = await TryCreateAuthorizedClientAsync(cancellationToken).ConfigureAwait(false);
            if (!clientResult.IsSuccess)
            {
                return new EmailSendResult
                {
                    Success = false,
                    StatusCode = clientResult.FailureStatusCode,
                    ResponseContent = string.Empty,
                    Message = clientResult.FailureMessage
                };
            }

            var client = clientResult.Client!;

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

            _logger.LogInternalInformation("Sending email via Outlook connector endpoint {Endpoint}", clientResult.BaseUrl);

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

    public async Task<EmailMessageResult> GetEmailAsync(
        string messageId,
        string? mailboxAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageId);

        string trimmedMessageId = messageId.Trim();
        if (string.IsNullOrWhiteSpace(trimmedMessageId))
        {
            return new EmailMessageResult
            {
                Success = false,
                StatusCode = 400,
                ResponseContent = string.Empty,
                Message = "A message ID must be provided."
            };
        }

        string? trimmedMailbox = string.IsNullOrWhiteSpace(mailboxAddress) ? null : mailboxAddress.Trim();

        try
        {
            var clientResult = await TryCreateAuthorizedClientAsync(cancellationToken).ConfigureAwait(false);
            if (!clientResult.IsSuccess)
            {
                return new EmailMessageResult
                {
                    Success = false,
                    StatusCode = clientResult.FailureStatusCode,
                    ResponseContent = string.Empty,
                    Message = clientResult.FailureMessage
                };
            }

            var client = clientResult.Client!;

            var requestUri = new StringBuilder($"v2/Mail/{Uri.EscapeDataString(trimmedMessageId)}");
            if (!string.IsNullOrEmpty(trimmedMailbox))
            {
                requestUri.Append("?mailboxAddress=").Append(Uri.EscapeDataString(trimmedMailbox));
            }

            using var response = await client.GetAsync(requestUri.ToString(), cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            bool success = response.IsSuccessStatusCode;
            var email = success ? TryParseEmail(responseBody) : null;

            if (success)
            {
                _logger.LogInternalInformation(
                    "Retrieved email {MessageId} with status code {StatusCode}",
                    trimmedMessageId,
                    (int)response.StatusCode);
            }
            else
            {
                _logger.LogInternalWarning(
                    "Failed to retrieve email {MessageId} with status code {StatusCode} and response {Response}",
                    trimmedMessageId,
                    (int)response.StatusCode,
                    responseBody);
            }

            return new EmailMessageResult
            {
                Success = success,
                StatusCode = (int)response.StatusCode,
                ResponseContent = responseBody,
                Message = success ? "Email retrieved successfully." : "Failed to retrieve email.",
                Email = email
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Unexpected error while retrieving email through Outlook connector.");
            return new EmailMessageResult
            {
                Success = false,
                StatusCode = 500,
                ResponseContent = string.Empty,
                Message = "Unexpected error while retrieving email."
            };
        }
    }

    public async Task<EmailListResult> ListEmailsAsync(
        string folderPath,
        bool fetchOnlyUnread,
        int top,
        string? mailboxAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folderPath);

        string trimmedFolderPath = folderPath.Trim();
        if (string.IsNullOrWhiteSpace(trimmedFolderPath))
        {
            return new EmailListResult
            {
                Success = false,
                StatusCode = 400,
                ResponseContent = string.Empty,
                Message = "Folder path must be provided."
            };
        }

        if (top <= 0)
        {
            return new EmailListResult
            {
                Success = false,
                StatusCode = 400,
                ResponseContent = string.Empty,
                Message = "The top parameter must be greater than zero."
            };
        }

        int cappedTop = Math.Min(top, MaxEmailListPageSize);
        if (cappedTop != top)
        {
            _logger.LogInternalInformation(
                "Requested top {RequestedTop} exceeds maximum {MaxTop}. Using capped value {CappedTop}.",
                top,
                MaxEmailListPageSize,
                cappedTop);
        }

        string? trimmedMailbox = string.IsNullOrWhiteSpace(mailboxAddress) ? null : mailboxAddress.Trim();

        try
        {
            var clientResult = await TryCreateAuthorizedClientAsync(cancellationToken).ConfigureAwait(false);
            if (!clientResult.IsSuccess)
            {
                return new EmailListResult
                {
                    Success = false,
                    StatusCode = clientResult.FailureStatusCode,
                    ResponseContent = string.Empty,
                    Message = clientResult.FailureMessage,
                    Emails = Array.Empty<EmailMessage>()
                };
            }

            var client = clientResult.Client!;

            var queryBuilder = new StringBuilder("v3/Mail?");
            queryBuilder
                .Append("folderPath=")
                .Append(Uri.EscapeDataString(trimmedFolderPath))
                .Append("&fetchOnlyUnread=")
                .Append(fetchOnlyUnread ? "true" : "false")
                .Append("&top=")
                .Append(cappedTop.ToString(CultureInfo.InvariantCulture));

            if (!string.IsNullOrEmpty(trimmedMailbox))
            {
                queryBuilder.Append("&mailboxAddress=").Append(Uri.EscapeDataString(trimmedMailbox));
            }

            using var response = await client.GetAsync(queryBuilder.ToString(), cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            bool success = response.IsSuccessStatusCode;
            string? continuationToken = null;
            IReadOnlyList<EmailMessage> emails = success
                ? TryParseEmailList(responseBody, out continuationToken)
                : Array.Empty<EmailMessage>();

            if (success)
            {
                _logger.LogInternalInformation(
                    "Retrieved {EmailCount} emails from folder {FolderPath} with status code {StatusCode}",
                    emails.Count,
                    trimmedFolderPath,
                    (int)response.StatusCode);
            }
            else
            {
                _logger.LogInternalWarning(
                    "Failed to list emails from folder {FolderPath} with status code {StatusCode} and response {Response}",
                    trimmedFolderPath,
                    (int)response.StatusCode,
                    responseBody);
            }

            return new EmailListResult
            {
                Success = success,
                StatusCode = (int)response.StatusCode,
                ResponseContent = responseBody,
                Message = success ? "Emails retrieved successfully." : "Failed to retrieve emails.",
                Emails = emails,
                ContinuationToken = continuationToken
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Unexpected error while listing emails through Outlook connector.");
            return new EmailListResult
            {
                Success = false,
                StatusCode = 500,
                ResponseContent = string.Empty,
                Message = "Unexpected error while retrieving emails."
            };
        }
    }

    public async Task<EmailReplyResult> ReplyToEmailAsync(
        string messageId,
        string body,
        string bodyType,
        string importance,
        string? mailboxAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentNullException.ThrowIfNull(body);

        string trimmedMessageId = messageId.Trim();
        string trimmedBody = body.Trim();

        if (string.IsNullOrWhiteSpace(trimmedMessageId) || string.IsNullOrWhiteSpace(trimmedBody))
        {
            return new EmailReplyResult
            {
                Success = false,
                StatusCode = 400,
                ResponseContent = string.Empty,
                Message = "Message ID and body must both be provided."
            };
        }

        string? trimmedMailbox = string.IsNullOrWhiteSpace(mailboxAddress) ? null : mailboxAddress.Trim();

        try
        {
            var clientResult = await TryCreateAuthorizedClientAsync(cancellationToken).ConfigureAwait(false);
            if (!clientResult.IsSuccess)
            {
                return new EmailReplyResult
                {
                    Success = false,
                    StatusCode = clientResult.FailureStatusCode,
                    ResponseContent = string.Empty,
                    Message = clientResult.FailureMessage
                };
            }

            var client = clientResult.Client!;

            var requestUri = new StringBuilder($"v3/Mail/ReplyTo/{Uri.EscapeDataString(trimmedMessageId)}");
            if (!string.IsNullOrEmpty(trimmedMailbox))
            {
                requestUri.Append("?mailboxAddress=").Append(Uri.EscapeDataString(trimmedMailbox));
            }

            var payload = new ReplyEmailPayload
            {
                Body = trimmedBody,
                BodyType = NormalizeBodyType(bodyType),
                Importance = NormalizeImportance(importance)
            };

            var content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

            _logger.LogInternalInformation(
                "Replying to email {MessageId} via Outlook connector endpoint {Endpoint}",
                trimmedMessageId,
                clientResult.BaseUrl);

            using var response = await client.PostAsync(requestUri.ToString(), content, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            bool success = response.IsSuccessStatusCode;
            if (success)
            {
                _logger.LogInternalInformation(
                    "Reply to email {MessageId} succeeded with status code {StatusCode}",
                    trimmedMessageId,
                    (int)response.StatusCode);
            }
            else
            {
                _logger.LogInternalWarning(
                    "Reply to email {MessageId} failed with status code {StatusCode} and response {Response}",
                    trimmedMessageId,
                    (int)response.StatusCode,
                    responseBody);
            }

            return new EmailReplyResult
            {
                Success = success,
                StatusCode = (int)response.StatusCode,
                ResponseContent = responseBody,
                Message = success ? "Reply sent successfully." : "Reply request failed."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Unexpected error while replying to email through Outlook connector.");
            return new EmailReplyResult
            {
                Success = false,
                StatusCode = 500,
                ResponseContent = string.Empty,
                Message = "Unexpected error while replying to email."
            };
        }
    }

    public async Task<EmailMoveResult> MoveEmailAsync(
        string messageId,
        string destinationFolderPath,
        string? mailboxAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentNullException.ThrowIfNull(destinationFolderPath);

        string trimmedMessageId = messageId.Trim();
        string trimmedFolderPath = destinationFolderPath.Trim();

        if (string.IsNullOrWhiteSpace(trimmedMessageId) || string.IsNullOrWhiteSpace(trimmedFolderPath))
        {
            return new EmailMoveResult
            {
                Success = false,
                StatusCode = 400,
                ResponseContent = string.Empty,
                Message = "Message ID and destination folder path must both be provided."
            };
        }

        string? trimmedMailbox = string.IsNullOrWhiteSpace(mailboxAddress) ? null : mailboxAddress.Trim();

        try
        {
            var clientResult = await TryCreateAuthorizedClientAsync(cancellationToken).ConfigureAwait(false);
            if (!clientResult.IsSuccess)
            {
                return new EmailMoveResult
                {
                    Success = false,
                    StatusCode = clientResult.FailureStatusCode,
                    ResponseContent = string.Empty,
                    Message = clientResult.FailureMessage
                };
            }

            var client = clientResult.Client!;

            var requestUri = new StringBuilder($"v3/Mail/Move/{Uri.EscapeDataString(trimmedMessageId)}?folderPath={Uri.EscapeDataString(trimmedFolderPath)}");
            if (!string.IsNullOrEmpty(trimmedMailbox))
            {
                requestUri.Append("&mailboxAddress=").Append(Uri.EscapeDataString(trimmedMailbox));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri.ToString());

            _logger.LogInternalInformation(
                "Moving email {MessageId} to {FolderPath} via Outlook connector endpoint {Endpoint}",
                trimmedMessageId,
                trimmedFolderPath,
                clientResult.BaseUrl);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            bool success = response.IsSuccessStatusCode;
            if (success)
            {
                _logger.LogInternalInformation(
                    "Email {MessageId} moved to {FolderPath} with status code {StatusCode}",
                    trimmedMessageId,
                    trimmedFolderPath,
                    (int)response.StatusCode);
            }
            else
            {
                _logger.LogInternalWarning(
                    "Failed to move email {MessageId} to {FolderPath} with status code {StatusCode} and response {Response}",
                    trimmedMessageId,
                    trimmedFolderPath,
                    (int)response.StatusCode,
                    responseBody);
            }

            return new EmailMoveResult
            {
                Success = success,
                StatusCode = (int)response.StatusCode,
                ResponseContent = responseBody,
                Message = success ? "Email moved successfully." : "Email move request failed."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Unexpected error while moving email through Outlook connector.");
            return new EmailMoveResult
            {
                Success = false,
                StatusCode = 500,
                ResponseContent = string.Empty,
                Message = "Unexpected error while moving email."
            };
        }
    }

    /// <summary>
    /// Check connectivity to the Outlook service by making a lightweight API call with top=1.
    /// </summary>
    public async Task<(bool Success, string ErrorMessage)> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInternalInformation("[CheckConnectivityAsync] Checking Outlook connectivity. Url: {Url}", _connector.Value.ConnectionRuntimeUrl);

            var clientResult = await TryCreateAuthorizedClientAsync(cancellationToken).ConfigureAwait(false);
            if (!clientResult.IsSuccess)
            {
                return (false, clientResult.FailureMessage);
            }

            var client = clientResult.Client!;

            using var response = await client.GetAsync("testconnection", cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInternalInformation("[CheckConnectivityAsync] Outlook connectivity check succeeded.");
                return (true, string.Empty);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInternalWarning("[CheckConnectivityAsync] Outlook connectivity check failed. StatusCode: {StatusCode}, Response: {Response}", response.StatusCode, errorContent);
            return (false, $"Outlook connectivity check failed with status code {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[CheckConnectivityAsync] Exception occurred while checking Outlook connectivity.");
            return (false, $"Exception occurred while checking Outlook connectivity: {ex.Message}");
        }
    }

    private async Task<HttpClientCreationResult> TryCreateAuthorizedClientAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var credential = _authenticationService.GetDataConnectorCredential(_connector.Value.Auth);
        var tokenRequest = new TokenRequestContext(DefaultConnectorScopes);
        var accessToken = await credential.GetTokenAsync(tokenRequest, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(accessToken.Token))
        {
            _logger.LogInternalError("Failed to acquire access token for Outlook connector.");
            return HttpClientCreationResult.Failure(401, "Failed to acquire access token for email connector.");
        }

        var client = _httpClientFactory.CreateClient();
        var baseUrl = EnsureTrailingSlash(_connector.Value.ConnectionRuntimeUrl);
        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

        return HttpClientCreationResult.Success(client, baseUrl);
    }

    private readonly struct HttpClientCreationResult
    {
        private HttpClientCreationResult(bool isSuccess, HttpClient? client, string? baseUrl, int failureStatusCode, string failureMessage)
        {
            IsSuccess = isSuccess;
            Client = client;
            BaseUrl = baseUrl;
            FailureStatusCode = failureStatusCode;
            FailureMessage = failureMessage;
        }

        public bool IsSuccess { get; }
        public HttpClient? Client { get; }
        public string? BaseUrl { get; }
        public int FailureStatusCode { get; }
        public string FailureMessage { get; }

        public static HttpClientCreationResult Success(HttpClient client, string baseUrl) => new(true, client, baseUrl, 0, string.Empty);

        public static HttpClientCreationResult Failure(int statusCode, string message) => new(false, null, null, statusCode, message);
    }

    private EmailMessage? TryParseEmail(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            return CreateEmailMessage(document.RootElement);
        }
        catch (JsonException ex)
        {
            _logger.LogInternalWarning("Failed to parse email payload: {Error}", ex.Message);
            return null;
        }
    }

    private IReadOnlyList<EmailMessage> TryParseEmailList(string responseContent, out string? continuationToken)
    {
        continuationToken = null;

        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return Array.Empty<EmailMessage>();
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            IReadOnlyList<EmailMessage> emails;
            if (root.ValueKind == JsonValueKind.Array)
            {
                emails = ParseEmailArray(root);
            }
            else if (TryGetPropertyCaseInsensitive(root, "value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
            {
                emails = ParseEmailArray(valueElement);
                continuationToken = ExtractContinuationToken(root);
            }
            else
            {
                var single = CreateEmailMessage(root);
                emails = single is null ? Array.Empty<EmailMessage>() : new[] { single };
            }

            continuationToken ??= ExtractContinuationToken(root);
            return emails;
        }
        catch (JsonException ex)
        {
            _logger.LogInternalWarning("Failed to parse email list payload: {Error}", ex.Message);
            continuationToken = null;
            return Array.Empty<EmailMessage>();
        }
    }

    private static IReadOnlyList<EmailMessage> ParseEmailArray(JsonElement arrayElement)
    {
        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EmailMessage>();
        }

        var emails = new List<EmailMessage>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            var email = CreateEmailMessage(item);
            if (email is not null)
            {
                emails.Add(email);
            }
        }

        return emails;
    }

    private static EmailMessage? CreateEmailMessage(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var rawClone = element.Clone();
        var id = GetStringCaseInsensitive(element, "Id") ?? GetStringCaseInsensitive(element, "id") ?? string.Empty;

        return new EmailMessage
        {
            Id = id,
            Subject = GetStringCaseInsensitive(element, "Subject") ?? GetStringCaseInsensitive(element, "subject"),
            From = ExtractSenderAddress(element),
            ToRecipients = ExtractRecipients(element, "ToRecipients"),
            CcRecipients = ExtractRecipients(element, "CcRecipients"),
            IsRead = GetNullableBool(element, "IsRead"),
            Importance = GetStringCaseInsensitive(element, "Importance"),
            ReceivedDateTime = GetNullableDateTime(element, "ReceivedDateTime"),
            BodyPreview = GetStringCaseInsensitive(element, "BodyPreview"),
            BodyContentType = GetNestedStringCaseInsensitive(element, "Body", "ContentType"),
            BodyContent = GetNestedStringCaseInsensitive(element, "Body", "Content"),
            RawPayload = rawClone
        };
    }

    private static string? ExtractContinuationToken(JsonElement element)
    {
        if (TryGetPropertyCaseInsensitive(element, "@odata.nextLink", out var nextLink) && nextLink.ValueKind == JsonValueKind.String)
        {
            return nextLink.GetString();
        }

        if (TryGetPropertyCaseInsensitive(element, "nextLink", out var altNextLink) && altNextLink.ValueKind == JsonValueKind.String)
        {
            return altNextLink.GetString();
        }

        if (TryGetPropertyCaseInsensitive(element, "skipToken", out var skipToken) && skipToken.ValueKind == JsonValueKind.String)
        {
            return skipToken.GetString();
        }

        return null;
    }

    private static string? GetStringCaseInsensitive(JsonElement element, string propertyName)
    {
        if (TryGetPropertyCaseInsensitive(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string? GetNestedStringCaseInsensitive(JsonElement element, string parentPropertyName, string childPropertyName)
    {
        if (TryGetPropertyCaseInsensitive(element, parentPropertyName, out var parent) && parent.ValueKind == JsonValueKind.Object)
        {
            return GetStringCaseInsensitive(parent, childPropertyName);
        }

        return null;
    }

    private static bool? GetNullableBool(JsonElement element, string propertyName)
    {
        if (TryGetPropertyCaseInsensitive(element, propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            {
                return property.GetBoolean();
            }
        }

        return null;
    }

    private static DateTimeOffset? GetNullableDateTime(JsonElement element, string propertyName)
    {
        var value = GetStringCaseInsensitive(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? ExtractSenderAddress(JsonElement element)
    {
        if (TryGetPropertyCaseInsensitive(element, "From", out var fromElement))
        {
            return ExtractRecipientAddress(fromElement);
        }

        if (TryGetPropertyCaseInsensitive(element, "Sender", out var senderElement))
        {
            return ExtractRecipientAddress(senderElement);
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractRecipients(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyCaseInsensitive(element, propertyName, out var recipientsElement) || recipientsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var recipients = new List<string>();
        foreach (var recipientElement in recipientsElement.EnumerateArray())
        {
            var address = ExtractRecipientAddress(recipientElement);
            if (!string.IsNullOrWhiteSpace(address))
            {
                recipients.Add(address);
            }
        }

        return recipients;
    }

    private static string? ExtractRecipientAddress(JsonElement element)
    {
        if (TryGetPropertyCaseInsensitive(element, "EmailAddress", out var emailAddressElement) && emailAddressElement.ValueKind == JsonValueKind.Object)
        {
            var nestedAddress = GetStringCaseInsensitive(emailAddressElement, "Address");
            if (!string.IsNullOrWhiteSpace(nestedAddress))
            {
                return nestedAddress;
            }
        }

        if (TryGetPropertyCaseInsensitive(element, "Address", out var addressElement) && addressElement.ValueKind == JsonValueKind.String)
        {
            return addressElement.GetString();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        return null;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out value))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string NormalizeBodyType(string bodyType)
    {
        if (string.IsNullOrWhiteSpace(bodyType))
        {
            return "HTML";
        }

        return string.Equals(bodyType.Trim(), "text", StringComparison.OrdinalIgnoreCase) ? "Text" : "HTML";
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

    private sealed class ReplyEmailPayload
    {
        [JsonPropertyName("body")]
        public string Body { get; init; } = string.Empty;

        [JsonPropertyName("body_type")]
        public string BodyType { get; init; } = "HTML";

        [JsonPropertyName("importance")]
        public string Importance { get; init; } = "Normal";
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
