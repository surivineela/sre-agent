using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;
public class ServiceNowIncidentManagementService : IncidentManagementServiceBase<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocument>
{
    protected override string DocumentType => "ServiceNowIncident";
    private readonly IServiceNowAPIClient _serviceNowAPIClient;

    public ServiceNowIncidentManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        ILogger<ServiceNowIncidentManagementService> logger,
        IIncidentFilterManagementService<ServiceNowIncidentFilterDocument> incidentFilterManagementService,
        IServiceNowAPIClient serviceNowAPIClient)
        : base(
          cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName),
          incidentFilterManagementService,
          logger)
    {
        _serviceNowAPIClient = serviceNowAPIClient;
    }

    public async override Task<ServiceNowIncidentDocument?> GetIncidentDetails(string incidentId)
    {
        _logger.LogInternalInformation(
            "GetIncidentDetails: Invoked for IncidentId: {IncidentId}",
            incidentId
        );
        try
        {
            // For ServiceNow, incidentId is the incident number
            // We need to get the sys_id first
            var sysId = await GetServiceNowSysId(incidentId);
            if (string.IsNullOrEmpty(sysId))
            {
                _logger.LogInternalWarning(
                    "GetIncidentDetails: Could not find sys_id for ServiceNow incident number: {IncidentNumber}",
                    incidentId
                );
                return default;
            }

            _logger.LogInternalInformation(
                "GetIncidentDetails: Found sys_id {SysId} for ServiceNow incident number: {IncidentNumber}",
                sysId, incidentId
            );

            // Now get the incident details using the sys_id
            var serviceNowResult = await GetIncidentDetailsInternal(incidentId);
            if (serviceNowResult == null)
            {
                _logger.LogInternalWarning(
                    "GetIncidentDetails: No incident found for ServiceNow incident number: {IncidentNumber}",
                    incidentId
                );
            }
            else
            {
                _logger.LogInternalInformation(
                    "GetIncidentDetails: Successfully retrieved incident for ServiceNow incident number: {IncidentNumber}",
                    incidentId
                );
            }
            return serviceNowResult;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "GetIncidentDetails: Exception occurred for ServiceNow incident number: {IncidentNumber}",
                incidentId
            );
            throw;
        }
    }


    public override async Task<IncidentQueryResult<ServiceNowIncidentDocument>> QueryIncidents(IncidentQueryRequest request)
    {
        return await QueryIncidentsInternal(request);
    }

    private async Task<string> GetServiceNowSysId(string incidentNumber)
    {
        _logger.LogInternalInformation(
            "GetServiceNowSysId: Retrieving sys_id for incident number: {IncidentNumber}",
            incidentNumber
        );

        try
        {
            if (_serviceNowAPIClient == null)
            {
                _logger.LogInternalError(
                    "GetServiceNowSysId: ServiceNowAPIClient is not initialized"
                );
                return string.Empty;
            }

            if (string.IsNullOrEmpty(incidentNumber))
            {
                _logger.LogInternalError(
                    "GetServiceNowSysId: Incident number is null or empty"
                );
                return string.Empty;
            }

            // Add debug logging to see what's being queried and returned
            _logger.LogInternalInformation(
                "GetServiceNowSysId: Querying for documents with DocumentType={DocumentType} and Number={Number}",
                "ServiceNowIncident", incidentNumber
            );

            // First, try to get the document from the database as it already contains the sys_id
            var query = _container.GetItemLinqQueryable<ServiceNowIncidentDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == "ServiceNowIncident" && c.Number == incidentNumber)
                .Take(1);

            _logger.LogInternalInformation(
                "GetServiceNowSysId: Query expression: {Query}",
                query.Expression.ToString()
            );

            var iterator = query.ToFeedIterator();

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var document = response.FirstOrDefault();

                if (document != null)
                {
                    _logger.LogInternalInformation(
                        "GetServiceNowSysId: Found document: Id={Id}, Number={Number}, IncidentSystemId={IncidentSystemId}",
                        document.Id, document.Number, document.IncidentSystemId
                    );

                    if (!string.IsNullOrEmpty(document.IncidentSystemId))
                    {
                        _logger.LogInternalInformation(
                            "GetServiceNowSysId: Found sys_id in document: {SysId} for incident number: {IncidentNumber}",
                            document.IncidentSystemId, incidentNumber
                        );
                        return document.IncidentSystemId;
                    }
                }
                else
                {
                    _logger.LogInternalWarning(
                        "GetServiceNowSysId: No document found with Number={Number}",
                        incidentNumber
                    );
                }
            }
            else
            {
                _logger.LogInternalWarning(
                    "GetServiceNowSysId: No results from query for Number={Number}",
                    incidentNumber
                );
            }

            // If we couldn't find the document in the database, query ServiceNow API
            var incidents = await _serviceNowAPIClient.GetIncidentsAsync(1, 0, null, null, null);
            var incident = incidents.FirstOrDefault(i => i.Number == incidentNumber);

            if (incident != null)
            {
                _logger.LogInternalInformation(
                    "GetServiceNowSysId: Found sys_id via API: {SysId} for incident number: {IncidentNumber}",
                    incident.IncidentId, incidentNumber
                );
                return incident.IncidentId;
            }

            _logger.LogInternalWarning(
                "GetServiceNowSysId: Could not find sys_id for incident number: {IncidentNumber}",
                incidentNumber
            );
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "GetServiceNowSysId: Exception occurred for incident number: {IncidentNumber}",
                incidentNumber
            );
            return string.Empty;
        }
    }

    protected override string[] NormalizeStatusesForFiltering(string[] statuses)
    {
        var normalizedStatuses = new List<string>();

        foreach (var status in statuses)
        {
            var lowerStatus = status.ToLower();
            normalizedStatuses.Add(lowerStatus);

            // For ServiceNow, also add numeric equivalents of common status names
            switch (lowerStatus)
            {
                case "new":
                    normalizedStatuses.Add("1");
                    break;
                case "active":
                case "in progress":
                case "work in progress":
                    normalizedStatuses.Add("2");
                    break;
                case "awaiting problem":
                    normalizedStatuses.Add("3");
                    break;
                case "awaiting user info":
                    normalizedStatuses.Add("4");
                    break;
                case "awaiting evidence":
                    normalizedStatuses.Add("5");
                    break;
                case "resolved":
                    normalizedStatuses.Add("6");
                    break;
                case "closed":
                    normalizedStatuses.Add("7");
                    break;
                case "cancelled":
                case "canceled":
                    normalizedStatuses.Add("8");
                    break;
                default:
                    // If it's already a numeric value, also add common names
                    switch (lowerStatus)
                    {
                        case "1":
                            normalizedStatuses.Add("new");
                            break;
                        case "2":
                            normalizedStatuses.Add("active");
                            normalizedStatuses.Add("in progress");
                            normalizedStatuses.Add("work in progress");
                            break;
                        case "3":
                            normalizedStatuses.Add("awaiting problem");
                            break;
                        case "4":
                            normalizedStatuses.Add("awaiting user info");
                            break;
                        case "5":
                            normalizedStatuses.Add("awaiting evidence");
                            break;
                        case "6":
                            normalizedStatuses.Add("resolved");
                            break;
                        case "7":
                            normalizedStatuses.Add("closed");
                            break;
                        case "8":
                            normalizedStatuses.Add("cancelled");
                            normalizedStatuses.Add("canceled");
                            break;
                    }
                    break;
            }
        }

        return normalizedStatuses.Distinct().ToArray();
    }

    protected override string[] NormalizePriorityForFiltering(string priority)
    {
        var normalizedPriorities = new List<string>();
        var lowerPriority = priority.ToLower();
        normalizedPriorities.Add(lowerPriority);

        // For ServiceNow, also add numeric equivalents of common priority names
        switch (lowerPriority)
        {
            case "critical":
            case "1 - critical":
                normalizedPriorities.Add("1");
                break;
            case "high":
            case "2 - high":
                normalizedPriorities.Add("2");
                break;
            case "moderate":
            case "medium":
            case "3 - moderate":
                normalizedPriorities.Add("3");
                break;
            case "low":
            case "4 - low":
                normalizedPriorities.Add("4");
                break;
            case "planning":
            case "5 - planning":
                normalizedPriorities.Add("5");
                break;
            default:
                // If it's already a numeric value, also add common names
                switch (lowerPriority)
                {
                    case "1":
                        normalizedPriorities.Add("critical");
                        break;
                    case "2":
                        normalizedPriorities.Add("high");
                        break;
                    case "3":
                        normalizedPriorities.Add("moderate");
                        normalizedPriorities.Add("medium");
                        break;
                    case "4":
                        normalizedPriorities.Add("low");
                        break;
                    case "5":
                        normalizedPriorities.Add("planning");
                        break;
                }
                break;
        }

        return normalizedPriorities.Distinct().ToArray();
    }
}
