// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.ICM;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class IcmIncidentFilterManagementService : IncidentFilterManagementServiceBase<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>
{
    private readonly IICMAPIClient _icmApiClient;
    public IcmIncidentFilterManagementService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IncidentManagementSettings incidentManagementSettings,
        ILogger<IcmIncidentFilterManagementService> logger,
        IICMAPIClient icmApiClient)
        : base(cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName
        ), logger, incidentManagementSettings)
    {
        _icmApiClient = icmApiClient;
    }

    public async override Task<bool> CheckConnectivity()
    {
        try
        {
            await _icmApiClient.GetIncidentsAsync(1, 0, null, null, null);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CheckConnectivity: Exception occurred while checking ICM connectivity.");
            return false;
        }
    }

    protected override Task<List<IncidentFilterFieldOption>> GetExtraFilterFieldOptions()
    {
        _logger.LogInternalInformation("ListIcmIncidentFilterFieldOptions: Invoked.");

        var result = new List<IncidentFilterFieldOption>();

        var incidentTypeOptions = new List<KeyValuePair<string, string>>();
        foreach (IncidentType incidentType in Enum.GetValues(typeof(IncidentType)))
        {
            incidentTypeOptions.Add(new KeyValuePair<string, string>(incidentType.ToString(), incidentType.ToString()));
        }

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(IcmIncidentDocument.IncidentType),
            DisplayName = "Incident Type",
            Options = incidentTypeOptions
        });

        var priorityOptions = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("1", "1"),
                    new KeyValuePair<string, string>("2", "2"),
                    new KeyValuePair<string, string>("25", "2.5"),
                    new KeyValuePair<string, string>("3", "3"),
                    new KeyValuePair<string, string>("4", "4")
                };
        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(IcmIncidentDocument.Priority),
            DisplayName = "Severity",
            Options = priorityOptions
        });

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(IcmIncidentDocument.OwningServiceId),
            DisplayName = "Owning Service Id",
            FieldInputType = IncidentFilterInputType.TextField
        });

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(IcmIncidentDocument.OwningTeam),
            DisplayName = "Owning Team Id",
            FieldInputType = IncidentFilterInputType.TextField,
            IsRequired = true
        });

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(IcmIncidentDocument.CreatedBy),
            DisplayName = "Created By",
            FieldInputType = IncidentFilterInputType.TextField
        });

        result.Add(new IncidentFilterFieldOption
        {
            FieldName = nameof(IcmIncidentDocument.MonitorId),
            DisplayName = "Monitor Id",
            FieldInputType = IncidentFilterInputType.TextField
        });

        _logger.LogInternalInformation("ListIcmIncidentFilterFieldOptions: Returning {OptionCount} field options.", result.Count);
        return Task.FromResult(result);
    }
}
