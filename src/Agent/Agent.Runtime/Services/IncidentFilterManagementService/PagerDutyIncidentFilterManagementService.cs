// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Json;
using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Runtime.Services;

public class PagerDutyIncidentFilterManagementService : IncidentFilterManagementServiceBase<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload>
{
    private readonly IPagerDutyService _pagerDutyService;
    public PagerDutyIncidentFilterManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IOptionsMonitor<IncidentManagementSettings> incidentManagementSettingsOptions,
        IPagerDutyService pagerDutyService,
        ILogger<PagerDutyIncidentFilterManagementService> logger)
        : base(cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName
        ), logger, incidentManagementSettingsOptions)
    {
        _pagerDutyService = pagerDutyService;
    }
    public async override Task<bool> CheckConnectivity()
    {
        try
        {
            var response = await _pagerDutyService.GetPagerDutyRequest("status");
            return response.IsSuccessStatusCode;
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
            // Get Impacted Services List
            _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Requesting PagerDuty services.");
            var servicesResponse = await _pagerDutyService.GetPagerDutyRequest("services");
            var services = await servicesResponse.Content.ReadFromJsonAsync<PDServicesResponse>();
            if (services != null && services.Services.Any())
            {
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {ServiceCount} services.", services.Services.Count);
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = nameof(PagerDutyIncidentFilterDocumentPayload.ImpactedService),
                    DisplayName = "Impacted Service",
                    Options = services.Services.Select(s => new KeyValuePair<string, string>(s.Id, s.Name)).ToList()
                });
            }
            else
            {
                _logger.LogInternalWarning("ListPagerDutyIncidentFilterFieldOptions: No services found in PagerDuty response.");
            }

            // Get Incident Types List
            _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Requesting PagerDuty incident types.");
            var incidentTypesResponse = await _pagerDutyService.GetPagerDutyRequest("incidents/types");
            var incidentTypes = await incidentTypesResponse.Content.ReadFromJsonAsync<PDIncidentTypesResponse>();
            if (incidentTypes != null && incidentTypes.IncidentTypes.Any())
            {
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {IncidentTypeCount} incident types.", incidentTypes.IncidentTypes.Count);
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = nameof(PagerDutyIncidentFilterDocumentPayload.IncidentType),
                    DisplayName = "Incident Type",
                    Options = incidentTypes.IncidentTypes.Select(it => new KeyValuePair<string, string>(it.Id, it.Name)).ToList()
                });
            }
            else
            {
                _logger.LogInternalWarning("ListPagerDutyIncidentFilterFieldOptions: No incident types found in PagerDuty response.");
            }

            // Get Priorities List
            _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Requesting PagerDuty priorities.");
            var prioritiesResponse = await _pagerDutyService.GetPagerDutyRequest("priorities");
            var priorities = await prioritiesResponse.Content.ReadFromJsonAsync<PDPrioritiesResponse>();
            if (priorities != null && priorities.Priorities.Any())
            {
                _logger.LogInternalInformation("ListPagerDutyIncidentFilterFieldOptions: Retrieved {PriorityCount} priorities.", priorities.Priorities.Count);
                result.Add(new IncidentFilterFieldOption
                {
                    FieldName = nameof(PagerDutyIncidentFilterDocumentPayload.Priority),
                    DisplayName = "Priority",
                    Options = priorities.Priorities.Select(p => new KeyValuePair<string, string>(p.Id, p.Name)).ToList()
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
