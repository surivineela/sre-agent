using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;
public class PagerDutyIncidentManagementService : IncidentManagementServiceBase<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocument>
{
    public PagerDutyIncidentManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentFilterManagementService<PagerDutyIncidentFilterDocument> incidentFilterManagementService,
        ILogger<PagerDutyIncidentManagementService> logger)
        : base(
            cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName),
            incidentFilterManagementService,
            logger)
    {
    }

    protected override string DocumentType => "PagerDutyIncident";

    public async override Task<PagerDutyIncidentDocument?> GetIncidentDetails(string incidentId)
    {
        try
        {
            return await GetIncidentDetailsInternal(incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred while fetching PagerDuty incident details for IncidentId: {IncidentId}", incidentId);
            throw;

        }
    }

    public override async Task<IncidentQueryResult<PagerDutyIncidentDocument>> QueryIncidents(IncidentQueryRequest request)
    {
        return await QueryIncidentsInternal(request);
    }
}
