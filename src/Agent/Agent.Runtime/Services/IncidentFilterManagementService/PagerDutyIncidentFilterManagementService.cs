// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class PagerDutyIncidentFilterManagementService : IncidentFilterManagementServiceBase<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload>
{
    private readonly IPagerDutyService _pagerDutyService;
    public PagerDutyIncidentFilterManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IncidentManagementSettings incidentManagementSettings,
        IPagerDutyService pagerDutyService,
        ILogger<PagerDutyIncidentFilterManagementService> logger)
        : base(cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName
        ), logger, incidentManagementSettings)
    {
        _pagerDutyService = pagerDutyService;
    }
    public async override Task<bool> CheckConnectivity()
    {
        try
        {
            var response = await _pagerDutyService.GetIncidentsAsync(1, 0);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CheckConnectivity: Exception occurred while checking PagerDuty connectivity.");
            return false;
        }
    }

    protected async override Task<List<IncidentFilterFieldOption>> GetExtraFilterFieldOptions()
    {
        _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Invoked.");

        var result = new List<IncidentFilterFieldOption>();
        try
        {
            // Run all three tasks in parallel and await their results
            var servicesTask = _pagerDutyService.GetPagerDutyServices();
            var prioritiesTask = _pagerDutyService.GetPagerDutyPriorities();
            var incidentTypesTask = _pagerDutyService.GetPagerDutyIncidentTypes();

            await Task.WhenAll(servicesTask, prioritiesTask, incidentTypesTask);

            var services = servicesTask.Result;
            var priorities = prioritiesTask.Result;
            var incidentTypes = incidentTypesTask.Result;

            if (services.Any())
            {
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {ServiceCount} services.", services.Count);
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = nameof(PagerDutyIncidentFilterDocumentPayload.ImpactedService),
                    DisplayName = "Impacted Service",
                    Options = services.Select(s => new KeyValuePair<string, string>(s.Id, s.Name)).ToList()
                });
            }
            else
            {
                _logger.LogInternalWarning("ListPagerDutyIncidentFilterFieldOptions: No services found in PagerDuty response.");
            }

            // Get Incident Types List
            _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Requesting PagerDuty incident types.");
            if (incidentTypes.Any())
            {
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {IncidentTypeCount} incident types.", incidentTypes.Count);
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = nameof(PagerDutyIncidentFilterDocumentPayload.IncidentType),
                    DisplayName = "Incident Type",
                    Options = incidentTypes.Select(it => new KeyValuePair<string, string>(it.Id, it.Name)).ToList()
                });
            }
            else
            {
                _logger.LogInternalWarning("ListPagerDutyIncidentFilterFieldOptions: No incident types found in PagerDuty response.");
            }

            // Get Priorities List
            _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Requesting PagerDuty priorities.");
            if (priorities.Any())
            {
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {PriorityCount} priorities.", priorities.Count);
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = nameof(PagerDutyIncidentFilterDocumentPayload.Priority),
                    DisplayName = "Priority",
                    Options = priorities.Select(p => new KeyValuePair<string, string>(p.Id, p.Name)).ToList()
                });
            }
            else
            {
                _logger.LogInternalWarning("ListPagerDutyIncidentFilterFieldOptions: No priorities found in PagerDuty response.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ListPagerDutyIncidentFilterFieldOptions: Exception occurred while retrieving PagerDuty field options.");
            throw;
        }
        _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Returning {OptionCount} field options.", result.Count);
        return result;
    }
}
