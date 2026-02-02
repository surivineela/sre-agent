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
        var result = await GetConnectivityStatus();
        return result.Success;
    }

    public async override Task<ConnectivityResult> GetConnectivityStatus()
    {
        try
        {
            _logger.LogInternalInformation("CheckConnectivity: Checking ServiceNow connection health...");

            var healthResult = await _serviceNowAPIClient.CheckConnectionHealthAsync();

            if (!healthResult.IsHealthy)
            {
                _logger.LogInternalWarning(
                    "CheckConnectivity: ServiceNow connection is not healthy. Status: {Status}, Error: {Error}",
                    healthResult.Status,
                    healthResult.ErrorMessage);
                return new ConnectivityResult(false, $"ServiceNow connection unhealthy. Status: {healthResult.Status}, Error: {healthResult.ErrorMessage}");
            }

            _logger.LogInternalInformation("CheckConnectivity: ServiceNow connection is healthy.");
            return new ConnectivityResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetConnectivityStatus: Exception occurred while checking ServiceNow connectivity.");
            return new ConnectivityResult(false, $"ServiceNow API call failed: {ex.Message}");
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

        var priorityDisplayNames = new Dictionary<string, string>
        {
            { "1", "Critical" },
            { "2", "High" },
            { "3", "Moderate" },
            { "4", "Low" },
            { "5", "Planning" }
        };
        var priorityOptions = IncidentPriorities.ServiceNow
            .Select(p => new KeyValuePair<string, string>(p, priorityDisplayNames.GetValueOrDefault(p, p)))
            .ToList();

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = "Priority",
            DisplayName = "Priority",
            Options = priorityOptions,
            FieldInputType = IncidentFilterInputType.MultiSelectDropdown,
            IsRequired = true
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
