// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class ServiceNowIncidentFilterManagementService : IncidentFilterManagementServiceBase<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>
{
    private readonly IServiceNowAPIClient _serviceNowAPIClient;
    public ServiceNowIncidentFilterManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IncidentManagementSettings incidentManagementSettings,
        IServiceNowAPIClient serviceNowAPIClient,
        ILogger<ServiceNowIncidentFilterManagementService> logger,
        IIncidentHandlerManagementService incidentHandlerManagementService)
        : base(cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName
        ), logger, incidentManagementSettings, incidentHandlerManagementService)
    {
        _serviceNowAPIClient = serviceNowAPIClient;
    }
    public async override Task<bool> CheckConnectivity()
    {
        try
        {
            await _serviceNowAPIClient.GetIncidentsAsync(1, 0, null, null, null);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CheckConnectivity: Exception occurred while checking ServiceNow connectivity.");
            return false;
        }
    }

    protected override Task<List<IncidentFilterFieldOption>> GetExtraFilterFieldOptions()
    {
        _logger.LogInternalInformation("ListServiceNowIncidentFilterFieldOptions: Invoked.");

        var result = new List<IncidentFilterFieldOption>();

        // In a real implementation, you would fetch these from ServiceNow API
        // For now, we'll use hardcoded values similar to the ICM implementation

        var incidentTypeOptions = new List<KeyValuePair<string, string>>();
        incidentTypeOptions.Add(new KeyValuePair<string, string>("ServiceNow", "ServiceNow"));

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(ServiceNowIncidentFilterDocumentPayload.IncidentType),
            DisplayName = "Incident Type",
            Options = incidentTypeOptions
        });

        var priorityOptions = new List<KeyValuePair<string, string>>
            {
                new("1", "Critical"),
                new("2", "High"),
                new("3", "Moderate"),
                new("4", "Low"),
                new("5", "Planning")
            };

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(ServiceNowIncidentFilterDocumentPayload.Priority),
            DisplayName = "Priority",
            Options = priorityOptions
        });

        // In a real implementation, you would fetch service list from ServiceNow
        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(ServiceNowIncidentFilterDocumentPayload.ImpactedService),
            DisplayName = "Impacted Service",
            Options = new List<KeyValuePair<string, string>>()
        });

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(ServiceNowIncidentFilterDocumentPayload.TitleContains),
            DisplayName = "Title Contains",
            FieldInputType = IncidentFilterInputType.TextField
        });

        _logger.LogInternalInformation("ListServiceNowIncidentFilterFieldOptions: Returning {OptionCount} field options.", result.Count);
        return Task.FromResult(result);
    }
}
