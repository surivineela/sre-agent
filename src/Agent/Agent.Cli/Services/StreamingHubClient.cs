using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Agent.Cli.Services;
using Agent.Cli.Models;
using System.Text.Json.Serialization;

namespace Agent.Cli.Services;

/// <summary>
/// Minimal SignalR streaming client used by the CLI for interactive chat.
/// </summary>
public sealed class StreamingHubClient : IAsyncDisposable
{
    private readonly ApiService _apiService;
    private readonly CliConfigurationService _configService;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public StreamingHubClient()
    {
        _apiService = new ApiService();
        _configService = new CliConfigurationService();
    }

    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        var config = await _configService.LoadConfigurationAsync();
        if (config == null || string.IsNullOrWhiteSpace(config.ResourceUrl))
        {
            return false;
        }

        var endpoint = config.ResourceUrl.TrimEnd('/') + "/agentHub";
        var isLocalhost = CliConfigurationService.IsLocalhost(config.ResourceUrl);

        async Task<string?> GetToken()
        {
            if (isLocalhost) return string.Empty;
            return await _apiService.GetAccessTokenForInternalUseAsync();
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(endpoint, options =>
            {
                options.AccessTokenProvider = async () => await GetToken() ?? string.Empty;
            })
            .WithAutomaticReconnect()
            .Build();

        try
        {
            await _connection.StartAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void OnMessageUpdate(Action<StreamingMessage> handler)
    {
        _connection?.On("MessageUpdate", (StreamingMessage message) => handler(message));
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            try { await _connection.StopAsync(); } catch { }
            await _connection.DisposeAsync();
        }
        _apiService.Dispose();
    }
}

#region DTOs compatible with web Streaming contracts

public class StreamingMessage
{
    public string? finishReason { get; set; }
    public string? authorName { get; set; }
    public string? role { get; set; }
    public StreamingMessageContent[]? contents { get; set; }
    public string? createdAt { get; set; }
    public AdditionalProperties? additionalProperties { get; set; }
}

public class StreamingMessageContent
{
    [JsonPropertyName("$type")] public string? Type { get; set; }
    public string? text { get; set; }
    public string? name { get; set; }
    public ContentAdditionalProperties? additionalProperties { get; set; }
}

public class AdditionalProperties
{
    public string? actionName { get; set; }
    public string? connectionId { get; set; }
    public string? threadId { get; set; }
    public string? messageId { get; set; }
    public string? streamMessageType { get; set; }
    public bool? isCancelled { get; set; }
    public string? userId { get; set; }
}

public class ContentAdditionalProperties
{
    public string? userDescription { get; set; }
    public string? functionCallDescription { get; set; }
}

internal static class StreamingMessageUtils
{
    public static string? ExtractText(StreamingMessage? message)
    {
        if (message?.contents == null) return null;
        foreach (var c in message.contents)
        {
            if (string.Equals(c?.Type, "text", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c?.text))
            {
                return c.text;
            }
        }
        return null;
    }
}

#endregion
