// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Core.Configuration;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DataModels;
using Agent.Plugins.Definitions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

public class IncidentPlugin(ILogger<IncidentPlugin> logger, 
							IGraphDatabaseClient graphDatabaseClient,
							CosmosDBSettings cosmosDbSettings,
							CosmosClient cosmosClient) : IIncidentPlugin
{
	private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, PagerDutyIncidentDocument.ContainerName);
	public async Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentsAsync(string resourceId, uint maxResults = 5)
	{
		logger.LogInformation("GetPagerDutyIncidentsAsync called with resourceId: {ResourceId}", resourceId);
		if (string.IsNullOrEmpty(resourceId))
		{
			logger.LogWarning("ResourceId is null or empty.");
			return [];
		}
		var query = $"g.V().has('resourceId', '{resourceId}').out('RELATED_TO_INCIDENT').has('resourceType', '/incidents/pagerduty').has('incidentId').project('incidentId').by('incidentId')";
		logger.LogInformation("Found {n} incidents for resourceId: {ResourceId}", query, resourceId);

		var result = await graphDatabaseClient.Query<Dictionary<string, object>>(query);
		List<string> incidentIds = result
			.Select(x => x["incidentId"]?.ToString() ?? string.Empty)
			.Where(incidentId => !string.IsNullOrEmpty(incidentId))
			.ToList();

		return await GetIncidentById(incidentIds, maxResults);
	}

	private async Task<List<PagerDutyIncidentDocument>> GetIncidentById(List<string> incidentId, uint maxResults)
	{
		var iterator = container.GetItemLinqQueryable<PagerDutyIncidentDocument>()
			.Where(doc => doc.DocumentType == "PagerDutyIncident" && incidentId.Contains(doc.Id))
			.OrderByDescending(doc => doc.CreatedAt)
			.Take((int)maxResults)
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

		return incidents;
	}
}
