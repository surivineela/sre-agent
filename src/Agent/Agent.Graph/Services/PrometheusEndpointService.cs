// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Services
{
    public interface IPrometheusEndpointService
    {
        Task<string?> GetPrometheusEndpointAsync(string aksResourceId);
    }

    public class PrometheusEndpointService : IPrometheusEndpointService
    {
        private readonly IGraphDatabaseClient _graphDbClient;
        private readonly ILogger<PrometheusEndpointService> _logger;

        public PrometheusEndpointService(IGraphDatabaseClient graphDbClient, ILogger<PrometheusEndpointService> logger)
        {
            _graphDbClient = graphDbClient;
            _logger = logger;
        }

        public async Task<string?> GetPrometheusEndpointAsync(string aksResourceId)
        {
            try
            {
                _logger?.LogInternalInformation("Looking for Azure Monitor Workspace connected to AKS cluster {ResourceId}", aksResourceId);

                // Query to find Azure Monitor Workspace nodes that have an edge from the AKS cluster with relationship type "MonitoredBy"
                var query = $@"g.V().has('resourceId', '{aksResourceId.ToLowerInvariant()}').has('isDeleted', false)
                             .out('MONITORED_BY')
                             .hasLabel('microsoft.monitor/accounts').has('isDeleted', false)
                             .has('prometheusQueryEndpoint')
                             .values('prometheusQueryEndpoint')
                             .limit(1)";

                var result = await _graphDbClient.Query<string>(query);

                // Process the result from the Gremlin query
                if (result != null)
                {
                    // Iterate through the result set to find the endpoint
                    foreach (var item in result)
                    {
                        if (item != null)
                        {
                            string prometheusEndpoint = item.ToString();
                            _logger?.LogInternalInformation("Found Prometheus query endpoint {PrometheusEndpoint} for AKS cluster {ResourceId}",
                                prometheusEndpoint, aksResourceId);
                            return prometheusEndpoint;
                        }
                    }
                }

                _logger?.LogInternalInformation("No Azure Monitor Workspace with Prometheus endpoint found for AKS cluster {ResourceId}", aksResourceId);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to get Prometheus endpoint for AKS resource {ResourceId}", aksResourceId);
                return null;
            }
        }
    }
}
