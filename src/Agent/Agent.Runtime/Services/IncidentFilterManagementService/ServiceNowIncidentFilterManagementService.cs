using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;
public class ServiceNowIncidentFilterManagementService : IncidentFilterManagementServiceBase<ServiceNowIncidentFilterDocument>
{
    private readonly IServiceNowAPIClient _serviceNowAPIClient;
    private readonly ILogger<ServiceNowIncidentFilterManagementService> _logger;
    public ServiceNowIncidentFilterManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IncidentManagementSettings incidentManagementSettings,
        IServiceNowAPIClient serviceNowAPIClient,
        ILogger<ServiceNowIncidentFilterManagementService> logger)
        : base(cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName
        ), logger, incidentManagementSettings)
    {
        _serviceNowAPIClient = serviceNowAPIClient;
        _logger = logger;
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
    public override Task<List<IncidentFilterFieldOption>> ListIncidentFilterFieldOptions()
    {
        _logger.LogInternalInformation("ListServiceNowIncidentFilterFieldOptions: Invoked.");

        var result = new List<IncidentFilterFieldOption>();

        // In a real implementation, you would fetch these from ServiceNow API
        // For now, we'll use hardcoded values similar to the ICM implementation

        var incidentTypeOptions = new List<KeyValuePair<string, string>>();
        incidentTypeOptions.Add(new KeyValuePair<string, string>("ServiceNow", "ServiceNow"));

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = "IncidentType",
            DisplayName = "Incident Type",
            Options = incidentTypeOptions
        });

        var priorityOptions = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("1", "Critical"),
                new KeyValuePair<string, string>("2", "High"),
                new KeyValuePair<string, string>("3", "Moderate"),
                new KeyValuePair<string, string>("4", "Low"),
                new KeyValuePair<string, string>("5", "Planning")
            };

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = "Priority",
            DisplayName = "Priority",
            Options = priorityOptions
        });

        // In a real implementation, you would fetch service list from ServiceNow
        result.Add(new IncidentFilterFieldOption
        {
            FieldName = "ImpactedService",
            DisplayName = "Impacted Service",
            Options = new List<KeyValuePair<string, string>>()
        });

        _logger.LogInternalInformation("ListServiceNowIncidentFilterFieldOptions: Returning {OptionCount} field options.", result.Count);
        return Task.FromResult(result);
    }
}
