// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Data.Interface.IncidentAPI;
using Agent.Runtime.Reasoning;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class AzMonitorIncidentFilterManagementService : IncidentFilterManagementServiceBase<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>
{
    private readonly IAzMonitorAlertService _azMonitorAlertService;
    public AzMonitorIncidentFilterManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IncidentManagementSettings incidentManagementSettings,
        ILogger<AzMonitorIncidentFilterManagementService> logger,
        IAzMonitorAlertService azMonitorAlertService,
        IIncidentHandlerManagementService incidentHandlerManagementService)
        : base(cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName
        ), logger, incidentManagementSettings, incidentHandlerManagementService)
    {
        _azMonitorAlertService = azMonitorAlertService;
    }

    public async override Task<bool> CheckConnectivity()
    {
        var result = await GetConnectivityStatus();
        return result.Success;
    }

    public async override Task<ConnectivityResult> GetConnectivityStatus()
    {
        try
        {
            await _azMonitorAlertService.GetIncidentsAsync(1, 0);
            return new ConnectivityResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetConnectivityStatus: Connectivity check failed for AzMonitorIncidentFilterManagementService");
            return new ConnectivityResult(false, $"Azure Monitor API call failed: {ex.Message}");
        }
    }

    protected override Task<List<IncidentFilterFieldOption>> GetExtraFilterFieldOptions()
    {
        _logger.LogInternalInformation("[AzMonitorIncidentFilterManagementService] GetExtraFilterFieldOptions: Invoked.");

        var result = new List<IncidentFilterFieldOption>();

        var priorityOptions = IncidentPriorities.AzMonitor
            .Select(p => new KeyValuePair<string, string>(p, p))
            .ToList();

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(AzMonitorIncidentFilterDocument.Priority),
            DisplayName = "Severity",
            Options = priorityOptions
        });

        return Task.FromResult(result);
    }

    //Overriding ListIncidentFilters so that create and save a default filter if no filter exists
    public override async Task<List<AzMonitorIncidentFilterDocument>> ListIncidentFilters(bool includeDisabled = true)
    {
        //If there isn't any filter created, create a default filter
        var queryable = _container.GetItemLinqQueryable<AzMonitorIncidentFilterDocument>(allowSynchronousQueryExecution: false)
                .Where(c => c.DocumentType == DocumentType);

        var iterator = queryable.ToFeedIterator();
        var results = new List<AzMonitorIncidentFilterDocument>();
        try
        {
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            _logger.LogInternalInformation("ListIncidentFilters: Retrieved {FilterCount} filters.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ListIncidentFilters: Exception occurred while listing incident filters.");
            throw;
        }

        if (results is null || results.Count == 0)
        {
            var defaultFilter = new AzMonitorIncidentFilterDocument
            {
                Id = "quickstart_handler",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Priority = "Sev3",
                AgentMode = AgentModes.Autonomous.ToLowerInvariant(),
            };
            await SaveIncidentFilter(defaultFilter);
        }

        return await base.ListIncidentFilters(includeDisabled);
    }
}
