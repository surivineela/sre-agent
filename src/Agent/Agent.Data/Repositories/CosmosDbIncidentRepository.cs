using System.Net;using Agent.Core.Configuration;using Agent.Data.DatabaseClients.GraphDbClient;using Agent.Data.DataModels;using Agent.Data.Helpers;using Microsoft.Azure.Cosmos;using Microsoft.Azure.Cosmos.Linq;using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class CosmosDbIncidentRepository : IIncidentRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbIncidentRepository> _logger;
    private readonly string _databaseName;
    private readonly CosmosClient _client;

    public CosmosDbIncidentRepository(CosmosClient cosmosClient, string databaseName, ILogger<CosmosDbIncidentRepository> logger)
    {
        _logger = logger;
        _databaseName = databaseName;
        _client = cosmosClient;

        // azMon & pagerduty incidents are stored in the same container, the container name is both "documents"
        _container = _client.GetContainer(_databaseName, PagerDutyIncidentDocument.ContainerName);
    }

    public async Task<List<PagerDutyIncidentDocument>> GetAllPagerDutyIncidentsAsync()
    {
        _logger.LogInformation("Fetching all PagerDuty incidents from Cosmos DB.");

        var iterator = _container.GetItemLinqQueryable<PagerDutyIncidentDocument>()
            .Where(doc => doc.DocumentType == "PagerDutyIncident")
            .OrderByDescending(doc => doc.CreatedAt)
            .ToFeedIterator();

        var incidents = new List<PagerDutyIncidentDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                incidents.Add(item);
            }
        }

        _logger.LogInformation("Fetched {Count} PagerDuty incidents from Cosmos DB.", incidents.Count);
        return incidents;
    }

    public async Task<List<AzMonitorAlertDocument>> GetAllAzMonIncidentsAsync()
    {
        _logger.LogInformation("Fetching all AzMon incidents from Cosmos DB.");

        var iterator = _container.GetItemLinqQueryable<AzMonitorAlertDocument>()
            .Where(doc => doc.DocumentType == "AzMonitorAlert")
            .OrderByDescending(doc => doc.CreatedAt)
            .ToFeedIterator();

        var incidents = new List<AzMonitorAlertDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                incidents.Add(item);
            }
        }

        _logger.LogInformation("Fetched {Count} AzMon incidents from Cosmos DB.", incidents.Count);
        return incidents;
    }
}
