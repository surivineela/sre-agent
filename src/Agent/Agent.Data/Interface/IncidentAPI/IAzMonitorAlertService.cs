// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Azure.ResourceManager.AlertsManagement.Models;

namespace Agent.Data.Interface.IncidentAPI;

public interface IAzMonitorAlertService : IIncidentAPI<AlertItem, AzMonitorIncidentFilterDocumentPayload>
{
    Task<bool> UpdateAlertStatus(string alertId, ServiceAlertState alertState);

    Task<bool> AcknowledgeAlert(string alertId);

    Task<bool> ResolveAlert(string alertId);
}
