using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager.Monitor.Models;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;

namespace Agent.Graph.Services;

public class WatchEventSource
{
    public string SubscriptionId { get; set; }
    public string? ResourceGroupName { get; set; }
    public ResourceIdentifier? ResourceId { get; set; }

    public string ToQueryString()
    {
        if (ResourceId != null)
        {
            return $"resourceUri eq '{ResourceId.ToString()}'";
        }

        if (!string.IsNullOrEmpty(ResourceGroupName))
        {
            return $"resourceGroupName eq '{ResourceGroupName}'";
        }

        return string.Empty;
    }
}

public class ActivityLogService : IWatchEventService
{
    private readonly ILogger<ActivityLogService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cache;

    public ActivityLogService(ILogger<ActivityLogService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cache = new ConcurrentDictionary<string, DateTimeOffset>();
    }

    public async IAsyncEnumerable<WatchEvent> WatchEvents(List<WatchEventSource> sources, CancellationToken? cancellationToken = null)
    {
        var eventCh = Channel.CreateUnbounded<EventDataInfo>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        foreach (var source in sources)
        {
            _ = Task.Run(async () => await ListActivityLogAsync(source, eventCh, 10, 1), cancellationToken ?? CancellationToken.None);
        }

        while (cancellationToken == null || !cancellationToken.Value.IsCancellationRequested)
        {
            while (await eventCh.Reader.WaitToReadAsync(cancellationToken ?? CancellationToken.None))
            {
                if (eventCh.Reader.TryRead(out var eventData))
                {
                    yield return new WatchEvent
                    {
                        EventData = eventData,
                    };
                }
            }
        }
    }

    private async Task ListActivityLogAsync(WatchEventSource source, Channel<EventDataInfo> eventCh, int lookback = 10, int interval = 1)
    {
        var lastTime = DateTimeOffset.UtcNow;
        while (true)
        {
            var endTime = DateTimeOffset.UtcNow;
            var startTime = endTime.AddMinutes(-lookback);
            if (DateTimeOffset.Compare(lastTime, startTime) <= 0)
            {
                startTime = lastTime;
            }
            var query = $"eventTimestamp ge '{startTime.UtcDateTime.ToString("O")}' and eventTimestamp le '{endTime.UtcDateTime.ToString("O")}'";
            var additionalFilters = source.ToQueryString();
            if (!string.IsNullOrEmpty(additionalFilters))
            {
                query += $" and {additionalFilters}";
            }

            var url = $"https://management.azure.com/subscriptions/{source.SubscriptionId}/providers/Microsoft.Insights/eventtypes/management/values?api-version=2015-04-01&$filter={query}";

            try
            {
                await foreach (var eventData in ListActivityLogAsync(url))
                {
                    if (string.IsNullOrEmpty(eventData.Id) || eventData.EventTimestamp == null)
                    {
                        continue;
                    }

                    if (_cache.ContainsKey(eventData.Id) && DateTimeOffset.Compare(eventData.EventTimestamp!.Value, _cache[eventData.Id]) <= 0)
                    {
                        continue;
                    }

                    _cache[eventData.Id] = eventData.EventTimestamp!.Value;

                    await eventCh.Writer.WriteAsync(eventData);
                }

                lastTime = endTime;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Failed to list activity logs. Url: {url}");
            }

            // Wait for the next interval
            await Task.Delay(TimeSpan.FromMinutes(interval));
        }
    }

    // Azure.ResourceManager.Monitor package does not provide public API for activity logs
    // REST API reference: https://learn.microsoft.com/en-us/azure/azure-monitor/essentials/rest-activity-log
    private async IAsyncEnumerable<EventDataInfo> ListActivityLogAsync(string url)
    {
        _logger.LogDebug($"List activity log. Url: {url}");
        var client = _httpClientFactory.CreateClient("Crawler");

        string? nextLink = url;
        while (!string.IsNullOrEmpty(nextLink))
        {
            var request = new HttpRequestMessage(HttpMethod.Get, nextLink);
            nextLink = null;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jDoc = await JsonDocument.ParseAsync(response.Content.ReadAsStream(), new JsonDocumentOptions { MaxDepth = 256 });
            foreach (var property in jDoc.RootElement.EnumerateObject())
            {
                if (property.Name == "nextLink")
                {
                    nextLink = property.Value.GetString();
                }
                else if (property.Name == "value")
                {
                    foreach (var data in property.Value.EnumerateArray())
                    {
                        var eventData = JsonSerializer.Deserialize<EventDataInfo>(data.GetRawText(), new JsonSerializerOptions
                        {
                            Converters = { new JsonModelConverter() },
                        });

                        if (eventData != null)
                        {
                            yield return eventData;
                        }
                    }
                }
            }
        }
    }
}
