using System;
using System.Threading.Channels;
using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Graph.Interfaces;
using Agent.Logging;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CrawlerConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Graph.Services;

public class KubernetesWatchService : IWatchEventService
{
    private readonly ILogger<KubernetesWatchService> _logger;
    private readonly IKubernetesService _kubernetesService;

    public KubernetesWatchService(ILogger<KubernetesWatchService> logger,
        [FromKeyedServices("Crawler")] IKubernetesService kubernetesService)
    {
        _logger = logger;
        _kubernetesService = kubernetesService;
    }

    public async IAsyncEnumerable<WatchEvent> WatchEvents(List<WatchEventSource> sources, CancellationToken? cancellationToken = null)
    {
        var eventCh = Channel.CreateUnbounded<KubernetesEventData>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        foreach (var source in sources)
        {
            _ = Task.Run(async () => await ListKubernetesResources(source, eventCh, 5), cancellationToken ?? CancellationToken.None);
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
    private async Task ListKubernetesResources(WatchEventSource source, Channel<KubernetesEventData> eventCh, int interval = 1)
    {
        while (true)
        {
            try
            {
                // Use the resource ID from the source to get the Kubernetes cluster
                var resourceId = source.ResourceId?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(resourceId))
                {
                    _logger.LogInternalWarning($"No AKS resource ID provided for source: {source}.");
                    return;
                }

                _logger.LogDebug("Listing Kubernetes namespaces for resource: {ResourceId}", resourceId);

                // TODO: non-namespaced resources
                var namespaceList = await _kubernetesService.GetNamespacesAsync(resourceId);

                if (namespaceList?.Items != null)
                {
                    foreach (var ns in namespaceList.Items)
                    {
                        var namespaceName = ns.Metadata?.Name;
                        var eventData = new KubernetesEventData
                        {
                            K8sObject = ns,
                            SubscriptionId = source.SubscriptionId,
                            ResourceGroupName = source.ResourceGroupName,
                            ClusterResourceId = resourceId,
                            Namespace = null,
                            ResourceName = namespaceName,
                            Group = CrawlerConstants.KubernetesCoreGroup,
                            ApiVersion = CrawlerConstants.KubernetesV1Version,
                            Kind = CrawlerConstants.KubernetesNamespaceType,
                        };
                        if (!string.IsNullOrEmpty(namespaceName))
                        {
                            _logger.LogDebug("Found Kubernetes namespace: {Namespace}", namespaceName);
                            await eventCh.Writer.WriteAsync(eventData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Failed to list Kubernetes resources for source: {source}");
            }

            // Wait for the next interval
            await Task.Delay(TimeSpan.FromMinutes(interval));
        }
    }
}

public class KubernetesEventData
{
    public IKubernetesObject? K8sObject { get; set; }
    public string SubscriptionId { get; set; }
    public string ResourceGroupName { get; set; }
    public string ClusterResourceId { get; set; }
    public string? Namespace { get; set; }
    public string ResourceName { get; set; }
    public string Group { get; set; }
    public string ApiVersion { get; set; }
    public string Kind { get; set; }
}
